using Microsoft.EntityFrameworkCore;
using Pakar.Application.Common;
using Pakar.Application.DTOs;
using Pakar.Application.Interfaces;
using Pakar.Domain.Entities;
using Pakar.Domain.Services;
using Pakar.Infrastructure.Persistence;

namespace Pakar.Infrastructure.Services;

public sealed class FacilityAdminService(AppDbContext db, TimeProvider clock) : IFacilityAdminService
{
    public const string VideoConnectionType = "Video";
    public const string ConcurrentCreateMessage = "Another item was created at the same time. Please try again.";

    // 1. External IDs include removed rows so an ID used by an old AI configuration is never reused.
    // 2. Two admins creating at the same moment can pick the same ID; the unique index rejects one (409).

    // ---- Zones ----

    public async Task<IReadOnlyList<ZoneAdminDto>> ListZonesAsync(CancellationToken ct = default) =>
        await db.Zones.AsNoTracking().OrderBy(z => z.ExternalId).Select(ZoneToDtoExpression).ToListAsync(ct);

    public async Task<OperationResult<ZoneAdminDto>> GetZoneAsync(Guid id, CancellationToken ct = default)
    {
        var zone = await db.Zones.AsNoTracking().Where(z => z.Id == id).Select(ZoneToDtoExpression).FirstOrDefaultAsync(ct);
        return zone is null
            ? OperationResult<ZoneAdminDto>.NotFound("Zone not found.")
            : OperationResult<ZoneAdminDto>.Ok(zone);
    }

    public async Task<OperationResult<ZoneAdminDto>> CreateZoneAsync(ZoneRequest request, CancellationToken ct = default)
    {
        var errors = FacilityRules.ValidateZone(request.Name, request.Description, request.TotalCapacity, existingSpotCount: 0);
        if (errors.Count > 0)
            return OperationResult<ZoneAdminDto>.Invalid(errors);

        var zone = new ParkingZone
        {
            Name = request.Name!.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            TotalCapacity = request.TotalCapacity,
            ExternalId = (await db.Zones.IgnoreQueryFilters().MaxAsync(z => (int?)z.ExternalId, ct) ?? 0) + 1
        };
        db.Zones.Add(zone);

        if (!await TrySaveAsync(ct))
            return OperationResult<ZoneAdminDto>.Conflict(ConcurrentCreateMessage);

        return OperationResult<ZoneAdminDto>.Ok(ToDto(zone, spotCount: 0, cameraCount: 0));
    }

    public async Task<OperationResult<ZoneAdminDto>> UpdateZoneAsync(Guid id, ZoneRequest request, CancellationToken ct = default)
    {
        var zone = await db.Zones.FirstOrDefaultAsync(z => z.Id == id, ct);
        if (zone is null)
            return OperationResult<ZoneAdminDto>.NotFound("Zone not found.");

        var spotCount = await db.Spots.CountAsync(s => s.ZoneId == id, ct);
        var errors = FacilityRules.ValidateZone(request.Name, request.Description, request.TotalCapacity, spotCount);
        if (errors.Count > 0)
            return OperationResult<ZoneAdminDto>.Invalid(errors);

        zone.Name = request.Name!.Trim();
        zone.Description = request.Description?.Trim() ?? string.Empty;
        zone.TotalCapacity = request.TotalCapacity;
        await db.SaveChangesAsync(ct);

        var cameraCount = await db.Cameras.CountAsync(c => c.ZoneId == id, ct);
        return OperationResult<ZoneAdminDto>.Ok(ToDto(zone, spotCount, cameraCount));
    }

    public async Task<OperationResult<bool>> DeleteZoneAsync(Guid id, CancellationToken ct = default)
    {
        var zone = await db.Zones.Include(z => z.Cameras).FirstOrDefaultAsync(z => z.Id == id, ct);
        if (zone is null)
            return OperationResult<bool>.NotFound("Zone not found.");

        // The zone's cameras are removed with it so they can no longer send detections.
        var now = clock.GetUtcNow().UtcDateTime;
        zone.DeletedAt = now;
        foreach (var camera in zone.Cameras)
            camera.DeletedAt = now;

        await db.SaveChangesAsync(ct);
        return OperationResult<bool>.Ok(true);
    }

    // ---- Cameras (video sources) ----

    public async Task<IReadOnlyList<CameraAdminDto>> ListCamerasAsync(Guid? zoneId, CancellationToken ct = default)
    {
        var query = CameraQuery();
        if (zoneId is { } zone)
            query = query.Where(c => c.ZoneId == zone);
        return await query.OrderBy(c => c.ExternalId).Select(CameraToDtoExpression).ToListAsync(ct);
    }

    public async Task<OperationResult<CameraAdminDto>> GetCameraAsync(Guid id, CancellationToken ct = default)
    {
        var camera = await CameraQuery().Where(c => c.Id == id).Select(CameraToDtoExpression).FirstOrDefaultAsync(ct);
        return camera is null
            ? OperationResult<CameraAdminDto>.NotFound("Camera not found.")
            : OperationResult<CameraAdminDto>.Ok(camera);
    }

    public async Task<OperationResult<CameraAdminDto>> CreateCameraAsync(CameraRequest request, CancellationToken ct = default)
    {
        var errors = FacilityRules.ValidateCamera(request.Name, request.VideoFile);
        if (errors.Count > 0)
            return OperationResult<CameraAdminDto>.Invalid(errors);

        if (!await db.Zones.AnyAsync(z => z.Id == request.ZoneId, ct))
            return OperationResult<CameraAdminDto>.Invalid(nameof(request.ZoneId), "Target zone does not exist.");

        var camera = new Camera
        {
            Name = request.Name!.Trim(),
            StreamUrl = FacilityRules.NormalizeVideoFile(request.VideoFile),
            ConnectionType = VideoConnectionType,
            ApiKeyHash = "temp-hash",
            ZoneId = request.ZoneId,
            ExternalId = (await db.Cameras.IgnoreQueryFilters().MaxAsync(c => (int?)c.ExternalId, ct) ?? 0) + 1
        };
        db.Cameras.Add(camera);

        if (!await TrySaveAsync(ct))
            return OperationResult<CameraAdminDto>.Conflict(ConcurrentCreateMessage);

        return await GetCameraAsync(camera.Id, ct);
    }

    public async Task<OperationResult<CameraAdminDto>> UpdateCameraAsync(Guid id, CameraRequest request, CancellationToken ct = default)
    {
        var camera = await db.Cameras.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (camera is null)
            return OperationResult<CameraAdminDto>.NotFound("Camera not found.");

        var errors = FacilityRules.ValidateCamera(request.Name, request.VideoFile);
        if (errors.Count > 0)
            return OperationResult<CameraAdminDto>.Invalid(errors);

        if (!await db.Zones.AnyAsync(z => z.Id == request.ZoneId, ct))
            return OperationResult<CameraAdminDto>.Invalid(nameof(request.ZoneId), "Target zone does not exist.");

        camera.Name = request.Name!.Trim();
        camera.ZoneId = request.ZoneId;
        camera.StreamUrl = FacilityRules.NormalizeVideoFile(request.VideoFile);
        camera.ConnectionType = VideoConnectionType;
        await db.SaveChangesAsync(ct);

        return await GetCameraAsync(id, ct);
    }

    public async Task<OperationResult<bool>> DeleteCameraAsync(Guid id, CancellationToken ct = default)
    {
        var camera = await db.Cameras.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (camera is null)
            return OperationResult<bool>.NotFound("Camera not found.");

        camera.DeletedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        return OperationResult<bool>.Ok(true);
    }

    // ---- helpers ----

    private static readonly System.Linq.Expressions.Expression<Func<ParkingZone, ZoneAdminDto>> ZoneToDtoExpression =
        z => new ZoneAdminDto(z.Id, z.Name, z.Description, z.TotalCapacity, z.ExternalId, z.Spots.Count, z.Cameras.Count);

    private IQueryable<Camera> CameraQuery() => db.Cameras.AsNoTracking();

    private static readonly System.Linq.Expressions.Expression<Func<Camera, CameraAdminDto>> CameraToDtoExpression =
        c => new CameraAdminDto(c.Id, c.Name, c.ZoneId, c.Zone.Name, c.Zone.ExternalId, c.ExternalId,
            c.ConnectionType,
            // Cameras seeded before the video-source change hold an rtsp:// URL (with credentials).
            // It is never returned; the admin sets a video file name instead.
            c.StreamUrl.Contains("://") ? string.Empty : c.StreamUrl,
            c.IsActive,
            c.LastActiveTime);

    private static ZoneAdminDto ToDto(ParkingZone zone, int spotCount, int cameraCount) => new(
        zone.Id, zone.Name, zone.Description, zone.TotalCapacity, zone.ExternalId, spotCount, cameraCount);

    private async Task<bool> TrySaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return false;
        }
    }
}
