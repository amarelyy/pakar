using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pakar.Application.Common;
using Pakar.Application.Interfaces;
using Pakar.Application.Validation;
using Pakar.Domain.Entities;
using Pakar.Infrastructure.Persistence;
using Pakar.Shared.Dtos;

namespace Pakar.Infrastructure.Services;

public sealed class RoiService(AppDbContext db, TimeProvider clock) : IRoiService
{
    public async Task<OperationResult<CameraRoisDto>> GetAsync(Guid cameraId, CancellationToken ct)
    {
        var camera = await db.Cameras.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cameraId, ct);
        if (camera is null) return OperationResult<CameraRoisDto>.NotFound("Video source not found.");
        var spots = await db.Spots.AsNoTracking().Where(s => s.ZoneId == camera.ZoneId).OrderBy(s => s.ExternalId)
            .Select(s => new RoiSpotDto(s.Id, s.Name, s.ExternalId)).ToListAsync(ct);
        var rois = await db.SpotRois.AsNoTracking().Include(r => r.Spot)
            .Where(r => r.CameraId == cameraId && r.Spot.ZoneId == camera.ZoneId).OrderBy(r => r.Spot.ExternalId).ToListAsync(ct);
        return OperationResult<CameraRoisDto>.Ok(new(cameraId, camera.ExternalId, camera.ZoneId,
            rois.Select(r => new RoiSlotDto(r.SpotId, r.Spot.Name, r.Spot.ExternalId, Read(r.PointsJson), r.UpdatedAt)).ToList(), spots));
    }

    public async Task<OperationResult<CameraRoisDto>> ReplaceAsync(Guid cameraId, RoiRequest request, CancellationToken ct)
    {
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            try
            {
                var camera = await db.Cameras.FirstOrDefaultAsync(c => c.Id == cameraId, ct);
                if (camera is null) return OperationResult<CameraRoisDto>.NotFound("Video source not found.");
                var ids = await db.Spots.Where(s => s.ZoneId == camera.ZoneId).Select(s => s.Id).ToHashSetAsync(ct);
                var errors = RoiRules.Validate(request, ids);
                if (errors.Count > 0) return OperationResult<CameraRoisDto>.Invalid(errors);
                var existing = await db.SpotRois.Where(r => r.CameraId == cameraId).ToListAsync(ct);
                var keep = request.Slots!.Select(s => s.SpotId).ToHashSet();
                db.SpotRois.RemoveRange(existing.Where(r => !keep.Contains(r.SpotId)));
                foreach (var slot in request.Slots!)
                {
                    var roi = existing.FirstOrDefault(r => r.SpotId == slot.SpotId);
                    if (roi is null) { roi = new SpotRoi { CameraId = cameraId, SpotId = slot.SpotId }; db.SpotRois.Add(roi); }
                    roi.PointsJson = JsonSerializer.Serialize(slot.Points!.Select(p => p.Select(v => Math.Round(v, 6)).ToArray()));
                    roi.UpdatedAt = clock.GetUtcNow().UtcDateTime;
                }
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return await GetAsync(cameraId, ct);
            }
            catch (Exception ex) when (ex is DbUpdateException || ex is Npgsql.PostgresException { SqlState: "40001" })
            {
                await transaction.RollbackAsync(ct);
                db.ChangeTracker.Clear();
                return OperationResult<CameraRoisDto>.Conflict("Source or polygons changed concurrently. Reload and retry.");
            }
        });
    }

    public async Task<IReadOnlyList<WorkerSourceDto>> GetSourcesAsync(CancellationToken ct)
    {
        // IsActive is observed health, not an enable switch: inactive sources must be able to start.
        var cameras = await db.Cameras.AsNoTracking().Include(c => c.Zone).Include(c => c.Rois).ThenInclude(r => r.Spot)
            .Where(c => c.ConnectionType == "Video" && c.StreamUrl != "" && c.Rois.Any()).OrderBy(c => c.ExternalId).ToListAsync(ct);
        return cameras.Where(c => !c.StreamUrl.Contains('/') && !c.StreamUrl.Contains('\\') && !c.StreamUrl.Contains(':') && !c.StreamUrl.Contains(".."))
            .Select(c => new WorkerSourceDto(c.ExternalId, c.Zone.ExternalId, c.Name, c.StreamUrl,
                c.Rois.Where(r => r.Spot.ZoneId == c.ZoneId).OrderBy(r => r.Spot.ExternalId)
                    .Select(r => new WorkerSlotDto(r.Spot.ExternalId, r.Spot.Name, Read(r.PointsJson))).ToList(), c.Zone.TotalCapacity))
            .Where(c => c.Slots.Count > 0).ToList();
    }

    private static double[][] Read(string json) => JsonSerializer.Deserialize<double[][]>(json)!;
}
