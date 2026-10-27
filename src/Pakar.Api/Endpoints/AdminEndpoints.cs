using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Pakar.Api.Security;
using Pakar.Application.DTOs;
using Pakar.Domain.Entities;
using Pakar.Infrastructure.Persistence;

namespace Pakar.Api.Endpoints;

/// <summary>
/// Zone and camera management (FR 13, 14). Every route requires the Admin role.
/// Removing a zone or camera hides it but keeps its detection history.
/// </summary>
public static partial class AdminEndpoints
{
    private const string ConcurrentCreateMessage = "Another item was created at the same time. Please try again.";

    public static void MapAdminEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/api").RequireAuthorization(AuthPolicies.AdminOnly);

        admin.MapPost("/zones", async (AppDbContext db, ZoneDto dto) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Name) || dto.TotalCapacity <= 0)
                return Results.BadRequest("Name and valid Capacity are required.");

            var zone = new ParkingZone
            {
                Name = dto.Name.Trim(),
                TotalCapacity = dto.TotalCapacity,
                ExternalId = await NextZoneExternalIdAsync(db)
            };
            db.Zones.Add(zone);

            if (!await TrySaveAsync(db))
                return Results.Conflict(ConcurrentCreateMessage);

            // ExternalId is returned so it can be entered in the AI pipeline configuration.
            return Results.Created($"/api/zones/{zone.Id}", new { zone.Id, zone.Name, zone.ExternalId });
        }).WithTags("Admin - Zones");

        admin.MapDelete("/zones/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var zone = await db.Zones.Include(z => z.Cameras).FirstOrDefaultAsync(z => z.Id == id);
            if (zone is null) return Results.NotFound();

            // The zone's cameras are removed with it so they can no longer send detections.
            var now = DateTime.UtcNow;
            zone.DeletedAt = now;
            foreach (var camera in zone.Cameras)
                camera.DeletedAt = now;

            await db.SaveChangesAsync();
            return Results.NoContent();
        }).WithTags("Admin - Zones");

        admin.MapPost("/cameras", async (AppDbContext db, CameraDto dto) =>
        {
            if (!IPv4Regex().IsMatch(dto.IpAddress))
                return Results.BadRequest("Invalid IP Address format.");

            if (!await db.Zones.AnyAsync(z => z.Id == dto.ZoneId))
                return Results.BadRequest("Target Zone does not exist.");

            var camera = new Camera
            {
                Name = dto.Name.Trim(),
                ConnectionType = "IP",
                ApiKeyHash = "temp-hash",
                ZoneId = dto.ZoneId,
                ExternalId = await NextCameraExternalIdAsync(db)
            };
            db.Cameras.Add(camera);

            if (!await TrySaveAsync(db))
                return Results.Conflict(ConcurrentCreateMessage);

            return Results.Created($"/api/cameras/{camera.Id}", new { camera.Id, camera.Name, camera.ExternalId });
        }).WithTags("Admin - Cameras");

        admin.MapDelete("/cameras/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var camera = await db.Cameras.FirstOrDefaultAsync(c => c.Id == id);
            if (camera is null) return Results.NotFound();

            camera.DeletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).WithTags("Admin - Cameras");
    }

    // 1. External IDs include removed rows so an ID used by an old AI configuration is never reused.
    // 2. Two admins creating at the same moment can pick the same ID; the unique index rejects one (409).
    private static async Task<int> NextZoneExternalIdAsync(AppDbContext db) =>
        (await db.Zones.IgnoreQueryFilters().MaxAsync(z => (int?)z.ExternalId) ?? 0) + 1;

    private static async Task<int> NextCameraExternalIdAsync(AppDbContext db) =>
        (await db.Cameras.IgnoreQueryFilters().MaxAsync(c => (int?)c.ExternalId) ?? 0) + 1;

    private static async Task<bool> TrySaveAsync(AppDbContext db)
    {
        try
        {
            await db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return false;
        }
    }

    [GeneratedRegex(@"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$")]
    private static partial Regex IPv4Regex();
}
