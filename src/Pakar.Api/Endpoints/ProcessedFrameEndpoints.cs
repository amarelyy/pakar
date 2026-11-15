using Pakar.Application.Validation;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Pakar.Api.Realtime;
using Pakar.Api.Security;
using Pakar.Application.DTOs;
using Pakar.Infrastructure.Persistence;
using Pakar.Shared.Dtos;

namespace Pakar.Api.Endpoints;

public static class ProcessedFrameEndpoints
{
    public static void MapProcessedFrames(this WebApplication app)
    {
        app.MapGet("/api/parking/observations", (CurrentObservations state) => state.Snapshot());
        app.MapPost("/api/internal/processed-frame", async (HttpRequest request, AppDbContext db,
            CurrentObservations state, ProcessedFrameStore frames, IHubContext<ParkingHub> hub, CancellationToken ct) =>
        {
            // Bound the body before deserializing arrays/base64, including chunked requests.
            using var buffer = new MemoryStream();
            var chunk = new byte[16384]; int count;
            while ((count = await request.Body.ReadAsync(chunk, ct)) > 0)
            {
                if (buffer.Length + count > 768*1024) return Results.StatusCode(413);
                buffer.Write(chunk, 0, count);
            }
            ProcessedFrameDto? frame;
            try { frame = System.Text.Json.JsonSerializer.Deserialize<ProcessedFrameDto>(buffer.ToArray(), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)); }
            catch (System.Text.Json.JsonException) { return Results.BadRequest(); }
            if(ProcessedFrameRules.Validate(frame, DateTimeOffset.UtcNow) is {} error) return Results.BadRequest(error);
            var o=frame!.Observation;
            var camera = await db.Cameras.Include(c=>c.Zone).FirstOrDefaultAsync(c=>c.ExternalId==o.CameraId,ct);
            if (camera is null || camera.Zone.ExternalId!=o.AreaId || camera.StreamUrl!=frame.VideoFile) return Results.Conflict();
            var rois = await db.SpotRois.Include(r=>r.Spot).Where(r=>r.CameraId==camera.Id).ToListAsync(ct);
            var ids = rois.Select(r=>r.Spot.ExternalId).Order().ToArray();
            if (!ids.SequenceEqual(o.Slots.Select(s=>s.SpotId).Order()) || !ids.SequenceEqual(frame.Polygons.Select(s=>s.SpotId).Order())) return Results.Conflict();
            foreach(var roi in rois)
            {
                var polygon = frame.Polygons.Single(s=>s.SpotId==roi.Spot.ExternalId);
                var saved = System.Text.Json.JsonSerializer.Deserialize<double[][]>(roi.PointsJson)!;
                if (polygon.Polygon.Length!=saved.Length || polygon.Polygon.Where((p,i)=>p.Length!=2 || Math.Abs(p[0]-saved[i][0])>0.0000011 || Math.Abs(p[1]-saved[i][1])>0.0000011).Any()) return Results.Conflict();
            }
            if(!frames.TryPublish(frame)) return Results.Conflict();
            await hub.Clients.Group(ParkingHub.AllGroup).SendAsync("ObservationsUpdated",o,ct);
            return Results.Ok();
        });
        app.MapGet("/api/cameras/{id:guid}/processed-frame", async (Guid id, AppDbContext db, ProcessedFrameStore frames, HttpResponse response, CancellationToken ct) =>
        {
            response.Headers.CacheControl="private, no-store";
            var camera=await db.Cameras.FirstOrDefaultAsync(c=>c.Id==id,ct);
            if(camera is null || frames.Get(camera.ExternalId) is not {} frame || frame.VideoFile!=camera.StreamUrl) return Results.NotFound();
            return Results.Ok(frame);
        }).RequireAuthorization(AuthPolicies.AdminOnly);
    }
}
