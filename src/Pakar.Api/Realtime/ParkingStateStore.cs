using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pakar.Domain.Entities;
using Pakar.Domain.Services;
using Pakar.Infrastructure.Persistence;

namespace Pakar.Api.Realtime;

/// <summary>
/// State parkir terkini di memori (singleton) supaya client baru langsung dapat snapshot
/// dan backend bisa mendeteksi perubahan (diff) sebelum broadcast.
/// Akses DB via IServiceScopeFactory (karena ini singleton, jangan inject DbContext langsung).
/// </summary>
public sealed class ParkingStateStore
{
    private readonly object _lock = new();
    private readonly Dictionary<int, SpotView> _spots = new();
    private readonly Dictionary<int, CameraView> _cameras = new();
    private readonly Dictionary<int, ZoneInfo> _zoneInfoCache = new();
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>What the database knows about a zone: its configured capacity and the admin's manual status (FR 17).</summary>
    private readonly record struct ZoneInfo(int Capacity, ZoneOccupancyStatus? Override);

    public ParkingStateStore(IServiceScopeFactory scopeFactory) { _scopeFactory = scopeFactory; }

    /// <summary>Terapkan update dari kamera; return hanya slot yang statusnya benar-benar berubah + data untuk persist DB.</summary>
    public (List<SpotView> Changed, ZoneView Zone, CameraView Camera, bool CameraReactivated,
            List<(int SpotExtId, string Status, double Confidence, DateTimeOffset Ts)> DbDetections)
        Apply(IngestRequest req)
    {
        lock (_lock)
        {
            var changed = new List<SpotView>();
            var dbDetections = new List<(int, string, double, DateTimeOffset)>();
            foreach (var u in req.Spots)
            {
                var status = u.Status == "occupied" ? "occupied" : "available";
                var next = new SpotView(u.SpotId, u.SpotNumber, req.AreaId, status, u.Confidence, u.Timestamp);
                var firstTime = !_spots.ContainsKey(u.SpotId);
                var changedNow = !_spots.TryGetValue(u.SpotId, out var prev) || prev.Status != status;
                if (changedNow)
                    changed.Add(next);
                _spots[u.SpotId] = next;

                if (changedNow || (firstTime && req.Type == "heartbeat"))
                    dbDetections.Add((u.SpotId, status, u.Confidence, u.Timestamp));
            }

            var reactivated = !_cameras.TryGetValue(req.CameraId, out var oldCam) || !oldCam.Active;
            var cam = new CameraView(req.CameraId, req.AreaId, true, DateTimeOffset.UtcNow);
            _cameras[req.CameraId] = cam;

            return (changed, BuildZone(req.AreaId), cam, reactivated, dbDetections);
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

    public ParkingSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            // Zones with a manual status are listed even before any spot of theirs has been seen.
            var areas = _spots.Values.Select(s => s.AreaId)
                .Concat(_zoneInfoCache.Where(e => e.Value.Override is not null).Select(e => e.Key))
                .Distinct();
            var zones = areas.Select(BuildZone).ToList();
            return new ParkingSnapshot(_spots.Values.ToList(), zones, _cameras.Values.ToList());
        }
    }

    // FR11: hitung ulang okupansi zona setiap ada perubahan slot
    // TAHAP 3: baca kapasitas dari ParkingZone.TotalCapacity via ExternalId mapping.
    private ZoneView BuildZone(int areaId)
    {
        var inArea = _spots.Values.Where(s => s.AreaId == areaId).ToList();
        var occ = inArea.Count(s => s.Status == "occupied");

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
        var resolved = OccupancyCalculator.Resolve(cap, occ, info.Override);
        return new ZoneView(areaId, cap, occ, resolved.Available, resolved.Status.ToApiValue(), resolved.IsOverridden);
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
