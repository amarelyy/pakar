using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pakar.Application.DTOs;
using Pakar.Application.Interfaces;
using Pakar.Domain.Services;
using Pakar.Infrastructure.Persistence;

namespace Pakar.Api.Realtime;

/// <summary>
/// State parkir terkini di memori (singleton) supaya client baru langsung dapat snapshot
/// dan backend bisa mendeteksi perubahan (diff) sebelum broadcast.
/// Akses DB via IServiceScopeFactory (karena ini singleton, jangan inject DbContext langsung).
/// </summary>
public sealed class ParkingStateStore : IParkingObservations
{
    private readonly object _lock = new();
    private readonly Dictionary<int, SpotView> _spots = new();
    private readonly Dictionary<int, string> _lastRecordedStatus = new();
    private readonly Dictionary<int, CameraView> _cameras = new();
    private readonly Dictionary<int, ZoneInfo> _zoneInfoCache = new();
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>What the database knows about a zone: its configured capacity and the admin's manual status (FR 17).</summary>
    private readonly record struct ZoneInfo(int Capacity, ZoneOccupancyStatus? Override);

    public ParkingStateStore(IServiceScopeFactory scopeFactory) { _scopeFactory = scopeFactory; }

    /// <summary>Terapkan update dari kamera; return hanya slot yang statusnya benar-benar berubah + data untuk persist DB.</summary>
    public (List<SpotView> Readings, List<SpotView> Changed, ZoneView Zone, CameraView Camera, bool CameraReactivated,
            List<(int SpotExtId, string Status, double Confidence, DateTimeOffset Ts)> DbDetections)
        Apply(IngestRequest req)
    {
        lock (_lock)
        {
            var readings = new List<SpotView>();
            var changed = new List<SpotView>();
            var dbDetections = new List<(int, string, double, DateTimeOffset)>();
            foreach (var update in req.Spots)
            {
                _spots.TryGetValue(update.SpotId, out var previous);
                if (previous is not null && update.Timestamp <= previous.UpdatedAt)
                    continue;

                var reading = new SpotView(update.SpotId, update.SpotNumber, req.AreaId,
                    update.Status, update.Confidence, update.Timestamp);
                _spots[update.SpotId] = reading;
                readings.Add(reading);
                if (previous?.Status != reading.Status)
                    changed.Add(reading);

                // An unknown reading changes availability but does not overwrite the last known history.
                if (reading.Status != ObservedOccupancy.Unknown &&
                    _lastRecordedStatus.GetValueOrDefault(reading.SpotId) != reading.Status)
                {
                    _lastRecordedStatus[reading.SpotId] = reading.Status;
                    dbDetections.Add((reading.SpotId, reading.Status, reading.Confidence, reading.UpdatedAt));
                }
            }

            var reactivated = !_cameras.TryGetValue(req.CameraId, out var oldCam) || !oldCam.Active;
            var cam = new CameraView(req.CameraId, req.AreaId, true, DateTimeOffset.UtcNow);
            _cameras[req.CameraId] = cam;

            return (readings, changed, BuildZone(req.AreaId), cam, reactivated, dbDetections);
        }
    }

    // FR12: kamera dianggap inactive kalau tidak ada data > timeout
    public List<CameraView> MarkStaleCameras(TimeSpan timeout)
    {
        lock (_lock)
        {
            var now = DateTimeOffset.UtcNow;
            var flagged = new List<CameraView>();
            foreach (var (id, c) in _cameras.ToList())
            {
                if (c.Active && now - c.LastSeen > timeout)
                {
                    var off = c with { Active = false };
                    _cameras[id] = off;
                    flagged.Add(off);
                }
            }
            return flagged;
        }
    }

    public IReadOnlyDictionary<int, SpotView> Read()
    {
        lock (_lock)
            return new Dictionary<int, SpotView>(_spots);
    }

    public ParkingSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            // Zones with a manual status are listed even before any spot of theirs has been seen.
            var areas = _spots.Values.Select(s => s.AreaId)
                .Concat(_zoneInfoCache.Where(e => e.Value.Override is not null).Select(e => e.Key))
                .Distinct();
            var zones = areas.Select(BuildZone).ToList();
            var now = DateTimeOffset.UtcNow;
            var spots = _spots.Values.Select(spot => spot with
            {
                Status = ObservedOccupancy.StatusAt(spot.Status, spot.UpdatedAt, now)
            }).ToList();
            return new ParkingSnapshot(spots, zones, _cameras.Values.ToList());
        }
    }

    // FR11: hitung ulang okupansi zona setiap ada perubahan slot
    // TAHAP 3: baca kapasitas dari ParkingZone.TotalCapacity via ExternalId mapping.
    private ZoneView BuildZone(int areaId)
    {
        var inArea = _spots.Values.Where(s => s.AreaId == areaId).ToList();
        var now = DateTimeOffset.UtcNow;
        var statuses = inArea.Select(spot => ObservedOccupancy.StatusAt(spot.Status, spot.UpdatedAt, now)).ToList();
        var occ = statuses.Count(status => status == "occupied");
        var available = statuses.Count(status => status == "available");

        if (!_zoneInfoCache.TryGetValue(areaId, out var info))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var zone = db.Zones.AsNoTracking().FirstOrDefault(z => z.ExternalId == areaId);
                info = new ZoneInfo(zone != null && zone.TotalCapacity > 0 ? zone.TotalCapacity : inArea.Count, zone?.StatusOverride);
            }
            catch
            {
                info = new ZoneInfo(inArea.Count, null);
            }
            _zoneInfoCache[areaId] = info;
            _ = Task.Delay(TimeSpan.FromHours(1)).ContinueWith(_ =>
            {
                lock (_lock) _zoneInfoCache.Remove(areaId);
            });
        }

        // Same rule as the REST zone list (OccupancyCalculator), so both always agree, including a manual status (FR 17).
        var cap = OccupancyCalculator.EffectiveCapacity(info.Capacity, inArea.Count);
        var resolved = ObservedOccupancy.Resolve(cap, occ, available, info.Override);
        return new ZoneView(areaId, cap, occ, resolved.Available, resolved.Status, info.Override.HasValue);
    }

    /// <summary>Paksa refresh kapasitas dari DB (misal setelah admin edit zona via UI).</summary>
    public void InvalidateZoneCapacity(int areaId)
    {
        lock (_lock) _zoneInfoCache.Remove(areaId);
    }

    /// <summary>Re-reads the zone from the database and returns its current view; call after a manual status changed (FR 17).</summary>
    public ZoneView RefreshZone(int areaId)
    {
        lock (_lock)
        {
            _zoneInfoCache.Remove(areaId);
            return BuildZone(areaId);
        }
    }

    /// <summary>Loads the manual statuses saved earlier, so they apply right after a restart.</summary>
    public void PreloadOverrides()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var overridden = db.Zones.AsNoTracking().Where(z => z.StatusOverride != null).Select(z => z.ExternalId).ToList();
        lock (_lock)
        {
            foreach (var areaId in overridden)
            {
                _zoneInfoCache.Remove(areaId);
                BuildZone(areaId);
            }
        }
    }
}
