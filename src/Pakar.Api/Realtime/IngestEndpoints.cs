using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pakar.Domain.Entities;
using Pakar.Infrastructure.Persistence;

namespace Pakar.Api.Realtime;

public static class IngestEndpoints
{
    public static void MapParkingRealtime(this WebApplication app)
    {
        app.MapHub<ParkingHub>("/hubs/parking");

        app.MapGet("/api/parking/snapshot", (ParkingStateStore store) => store.GetSnapshot());

        app.MapPost("/api/ingest/detections", async (
            HttpRequest http,
            IngestRequest req,
            ParkingStateStore store,
            IHubContext<ParkingHub> hub,
            IConfiguration cfg,
            IServiceScopeFactory scopeFactory,
            ILogger<ParkingStateStore> log) =>
        {
            var expected = cfg["Ingest:ApiKey"] ?? "";
            var given = http.Headers["X-Api-Key"].ToString();
            if (expected.Length == 0 ||
                !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(given)))
                return Results.Unauthorized();

            if (req.Spots is null || req.Spots.Count > 500)
                return Results.BadRequest("spots kosong atau terlalu banyak");

            var (changed, zone, cam, reactivated, dbDetections) = store.Apply(req);

            var persistTask = PersistToDatabaseAsync(scopeFactory, req, dbDetections, log);

            foreach (var s in changed)
                await hub.Clients.Group(ParkingHub.AreaGroup(s.AreaId)).SendAsync("SpotStatusChanged", s);

            if (changed.Count > 0)
                await hub.Clients.Group(ParkingHub.AllGroup).SendAsync("ZoneUpdated", zone);

            if (reactivated)
                await hub.Clients.Group(ParkingHub.AllGroup).SendAsync("CameraStatusChanged", cam);

            log.LogInformation("ingest cam={Cam} type={Type} changed={N} zone {Occ}/{Cap}",
                req.CameraId, req.Type, changed.Count, zone.Occupied, zone.Capacity);

            await Task.WhenAny(persistTask, Task.CompletedTask);
            return Results.Ok(new { changed = changed.Count });
        });
    }

    private static async Task PersistToDatabaseAsync(
        IServiceScopeFactory scopeFactory,
        IngestRequest req,
        List<(int SpotExtId, string Status, double Confidence, DateTimeOffset Ts)> detections,
        ILogger log)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTimeOffset.UtcNow;

            var camera = await db.Cameras.FirstOrDefaultAsync(c => c.ExternalId == req.CameraId);
            if (camera != null)
            {
                camera.LastActiveTime = now;
            }
            else
            {
                log.LogWarning("Camera ExternalId={Cam} tidak ditemukan di DB; skip update last_active", req.CameraId);
            }

            if (detections.Count > 0)
            {
                var spotExtIds = detections.Select(d => d.SpotExtId).Distinct().ToList();
                var spotMap = await db.Spots
                    .Where(s => spotExtIds.Contains(s.ExternalId))
                    .ToDictionaryAsync(s => s.ExternalId);

                foreach (var d in detections)
                {
                    var detection = new Detection
                    {
                        Timestamp = d.Ts.UtcDateTime,
                        ConfidenceScore = (float)d.Confidence,
                        Status = d.Status,
                        SpotExternalId = d.SpotExtId,
                        BoundingBoxJson = string.Empty,
                        SnapshotUrl = string.Empty,
                        CameraId = camera?.Id ?? Guid.Empty,
                        SpotId = spotMap.TryGetValue(d.SpotExtId, out var sp) ? sp.Id : null,
                    };
                    db.Detections.Add(detection);

                    if (spotMap.TryGetValue(d.SpotExtId, out var spot))
                    {
                        spot.IsOccupied = d.Status == "occupied";
                        spot.LastUpdated = d.Ts.UtcDateTime;
                    }
                }
            }

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Gagal persist ingest cam={Cam} type={Type} ke DB", req.CameraId, req.Type);
        }
    }
}

/// <summary>FR12: tandai kamera inactive kalau tidak ada data selama 2 menit.</summary>
public sealed class CameraWatchdog : BackgroundService
{
    private readonly ParkingStateStore _store;
    private readonly IHubContext<ParkingHub> _hub;
    public CameraWatchdog(ParkingStateStore store, IHubContext<ParkingHub> hub) { _store = store; _hub = hub; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(ct))
            foreach (var cam in _store.MarkStaleCameras(TimeSpan.FromMinutes(2)))
                await _hub.Clients.Group(ParkingHub.AllGroup).SendAsync("CameraStatusChanged", cam, ct);
    }
}
