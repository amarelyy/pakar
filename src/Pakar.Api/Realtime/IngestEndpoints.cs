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
        app.MapHub<FrameHub>(FrameHub.Route);

        app.MapGet("/api/parking/snapshot", (ParkingStateStore store) => store.GetSnapshot());

        // Authenticated by CameraAuthMiddleware, which guards every /api/ingest route.
        app.MapPost("/api/ingest/detections", async (
            IngestRequest req,
            AppDbContext db,
            ParkingStateStore store,
            IHubContext<ParkingHub> hub,
            IServiceScopeFactory scopeFactory,
            ILogger<ParkingStateStore> log) =>
        {
            var receivedAt=DateTimeOffset.UtcNow;
            if (req.Spots is null || req.Spots.Count > 500 || req.CameraId<=0 || req.AreaId<=0 ||
                req.Type is not ("change" or "heartbeat") || req.Timestamp>receivedAt.AddSeconds(5) || req.Timestamp<receivedAt.AddMinutes(-2) ||
                req.Spots.Any(s=>s is null || s.SpotId<=0 || s.CameraId!=req.CameraId || s.AreaId!=req.AreaId ||
                    string.IsNullOrWhiteSpace(s.SpotNumber) || s.SpotNumber.Length>200 || s.Status is not ("occupied" or "available") ||
                    !double.IsFinite(s.Confidence) || s.Confidence is <0 or >1 || s.Timestamp>receivedAt.AddSeconds(5) || s.Timestamp<receivedAt.AddMinutes(-2)) ||
                req.Spots.Select(s=>s.SpotId).Distinct().Count()!=req.Spots.Count)
                return Results.BadRequest("Invalid live observation identifiers, states, confidence or timestamps.");

            // Every detection row references a camera, so data from unregistered cameras is rejected up front.
            var registered = await db.Cameras.AsNoTracking().Include(c=>c.Zone).FirstOrDefaultAsync(c=>c.ExternalId==req.CameraId);
            if (registered is null)
            {
                log.LogWarning("Rejected ingest from unregistered camera {Cam}.", req.CameraId);
                return Results.NotFound($"Camera {req.CameraId} is not registered.");
            }
            if(registered.Zone.ExternalId!=req.AreaId) return Results.BadRequest("Source does not belong to this area.");
            var ids=req.Spots.Select(s=>s.SpotId).ToArray();
            var names=await db.Spots.AsNoTracking().Where(s=>s.ZoneId==registered.ZoneId && ids.Contains(s.ExternalId))
                .ToDictionaryAsync(s=>s.ExternalId,s=>s.Name);
            if(req.Spots.Any(s=>!names.TryGetValue(s.SpotId,out var name) || name!=s.SpotNumber))
                return Results.BadRequest("Spot identifiers and names must belong to the source area.");

            var (changed, zone, cam, reactivated, dbDetections) = store.Apply(req);

            var persistTask = PersistToDatabaseAsync(scopeFactory, req, dbDetections, log);

            foreach (var s in changed)
                await hub.Clients.Group(ParkingHub.AreaGroup(s.AreaId)).SendAsync("SpotStatusChanged", s);

            if (changed.Count > 0)
                await hub.Clients.Group(ParkingHub.AllGroup).SendAsync("ZoneUpdated", zone);

            // Heartbeats refresh the map's freshness timestamp even when no slot changes.
            if (reactivated || req.Type == "heartbeat")
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

            // The camera may have been deleted after the request was validated.
            var camera = await db.Cameras.FirstOrDefaultAsync(c => c.ExternalId == req.CameraId);
            if (camera is null)
            {
                log.LogWarning("Camera {Cam} was removed before its detections were saved.", req.CameraId);
                return;
            }

            camera.LastActiveTime = now.UtcDateTime;
            camera.IsActive = true;

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
                        CameraId = camera.Id,
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
            log.LogError(ex, "Failed to save ingest from camera {Cam} (type {Type}).", req.CameraId, req.Type);
        }
    }
}

/// <summary>FR12: tandai kamera inactive kalau tidak ada data selama 2 menit.</summary>
public sealed class CameraWatchdog : BackgroundService
{
    private readonly ParkingStateStore _store;
    private readonly IHubContext<ParkingHub> _hub;
    private readonly IServiceScopeFactory scopes;
    private readonly ILogger<CameraWatchdog> logger;
    public CameraWatchdog(ParkingStateStore store, IHubContext<ParkingHub> hub, IServiceScopeFactory scopes, ILogger<CameraWatchdog> logger) { _store = store; _hub = hub; this.scopes=scopes;this.logger=logger; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var cutoff=DateTime.UtcNow.AddMinutes(-2);
                await db.Cameras.Where(c=>c.IsActive && (c.LastActiveTime==null || c.LastActiveTime<cutoff)).ExecuteUpdateAsync(set=>set.SetProperty(c=>c.IsActive,false),ct);
            }
            catch(Exception ex) when(ex is not OperationCanceledException) { logger.LogWarning("Source watchdog persistence failed ({Type}); will retry.",ex.GetType().Name); }
            foreach (var cam in _store.MarkStaleCameras(TimeSpan.FromMinutes(2)))
                await _hub.Clients.Group(ParkingHub.AllGroup).SendAsync("CameraStatusChanged", cam, ct);
        }
    }
}
