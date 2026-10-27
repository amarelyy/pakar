using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Pakar.Api.Security;
using Pakar.Application.DTOs;
using Pakar.Domain.Entities;
using Pakar.Infrastructure.Persistence;

namespace Pakar.Api.Endpoints;

/// <summary>Zone and camera management (FR 13, 14). Every route requires the Admin role.</summary>
public static partial class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/api").RequireAuthorization(AuthPolicies.AdminOnly);

        admin.MapPost("/zones", async (AppDbContext db, ZoneDto dto) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Name) || dto.TotalCapacity <= 0)
                return Results.BadRequest("Name and valid Capacity are required.");

            var zone = new ParkingZone { Name = dto.Name, TotalCapacity = dto.TotalCapacity };
            db.Zones.Add(zone);
            await db.SaveChangesAsync();

            return Results.Created($"/api/zones/{zone.Id}", new { zone.Id, zone.Name });
        }).WithTags("Admin - Zones");

        admin.MapDelete("/zones/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var zone = await db.Zones.FindAsync(id);
            if (zone is null) return Results.NotFound();

            db.Zones.Remove(zone);
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
                Name = dto.Name,
                ConnectionType = "IP",
                ApiKeyHash = "temp-hash",
                ZoneId = dto.ZoneId
            };

            db.Cameras.Add(camera);
            await db.SaveChangesAsync();
            return Results.Created($"/api/cameras/{camera.Id}", camera.Id);
        }).WithTags("Admin - Cameras");

        admin.MapDelete("/cameras/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var camera = await db.Cameras.FindAsync(id);
            if (camera is null) return Results.NotFound();

            db.Cameras.Remove(camera);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).WithTags("Admin - Cameras");
    }

    [GeneratedRegex(@"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$")]
    private static partial Regex IPv4Regex();
}
