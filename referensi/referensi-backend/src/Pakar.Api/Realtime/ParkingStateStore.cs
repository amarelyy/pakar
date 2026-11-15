using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pakar.Domain.Entities;
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
    private readonly Dictionary<int, int> _zoneCapacityCache = new();
    private readonly IServiceScopeFactory _scopeFactory;

    public const double AlmostFullRatio = 0.9;

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

            var reactivated = _cameras.TryGetValue(req.CameraId, out var oldCam) && !oldCam.Active;
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
            var zones = _spots.Values.Select(s => s.AreaId).Distinct().Select(BuildZone).ToList();
            return new ParkingSnapshot(_spots.Values.ToList(), zones, _cameras.Values.ToList());
        }
    }

    // FR11: hitung ulang okupansi zona setiap ada perubahan slot
    // TAHAP 3: baca kapasitas dari ParkingZone.TotalCapacity via ExternalId mapping.
    private ZoneView BuildZone(int areaId)
    {
        var inArea = _spots.Values.Where(s => s.AreaId == areaId).ToList();
        var occ = inArea.Count(s => s.Status == "occupied");

        int cap;
        if (_zoneCapacityCache.TryGetValue(areaId, out var cached))
        {
            cap = cached;
        }
        else
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var zone = db.Zones.AsNoTracking().FirstOrDefault(z => z.ExternalId == areaId);
                cap = zone != null && zone.TotalCapacity > 0 ? zone.TotalCapacity : inArea.Count;
            }
            catch
            {
                cap = inArea.Count;
            }
            _zoneCapacityCache[areaId] = cap;
            _ = Task.Delay(TimeSpan.FromHours(1)).ContinueWith(_ =>
            {
                lock (_lock) _zoneCapacityCache.Remove(areaId);
            });
        }

        cap = Math.Max(cap, inArea.Count);

        var status = cap == 0 ? "available"
                   : occ >= cap ? "full"
                   : (double)occ / cap >= AlmostFullRatio ? "almost_full"
                   : "available";
        return new ZoneView(areaId, cap, occ, cap - occ, status);
    }

    /// <summary>Paksa refresh kapasitas dari DB (misal setelah admin edit zona via UI).</summary>
    public void InvalidateZoneCapacity(int areaId)
    {
        lock (_lock) _zoneCapacityCache.Remove(areaId);
    }
}
