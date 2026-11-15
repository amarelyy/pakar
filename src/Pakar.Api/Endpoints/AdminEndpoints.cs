using Microsoft.AspNetCore.SignalR;
using Pakar.Api.Realtime;
using Pakar.Application.DTOs;
using Pakar.Infrastructure.Persistence;
using Pakar.Api.Security;
using Pakar.Application.Interfaces;

namespace Pakar.Api.Endpoints;

/// <summary>
/// Zone and video source (camera) management (FR 13, 14). Every route requires the Admin role.
/// Removing a zone or camera hides it but keeps its detection history.
/// </summary>
public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/api").RequireAuthorization(AuthPolicies.AdminOnly);

        // ---- Zones ----

        admin.MapGet("/zones", async (IFacilityAdminService service, CancellationToken ct) =>
            Results.Ok(await service.ListZonesAsync(ct))).WithTags("Admin - Zones");

        admin.MapGet("/zones/{id:guid}", async (Guid id, IFacilityAdminService service, CancellationToken ct) =>
            (await service.GetZoneAsync(id, ct)).ToHttpResult()).WithTags("Admin - Zones");

        admin.MapPost("/zones", async (ZoneRequest request, IFacilityAdminService service, CancellationToken ct) =>
            // ExternalId is returned so it can be entered in the AI worker configuration.
            (await service.CreateZoneAsync(request, ct))
                .ToHttpResult(zone => Results.Created($"/api/zones/{zone.Id}", zone))).WithTags("Admin - Zones");

        admin.MapPut("/zones/{id:guid}", async (Guid id, ZoneRequest request, IFacilityAdminService service,
                ParkingStateStore store, CancellationToken ct) =>
            (await service.UpdateZoneAsync(id, request, ct)).ToHttpResult(zone =>
            {
                // The realtime store caches capacity per zone; refresh it so the map uses the new value.
                store.InvalidateZoneCapacity(zone.ExternalId);
                return Results.Ok(zone);
            })).WithTags("Admin - Zones");

        admin.MapDelete("/zones/{id:guid}", async (Guid id, IFacilityAdminService service, CancellationToken ct) =>
            (await service.DeleteZoneAsync(id, ct)).ToHttpResult(_ => Results.NoContent())).WithTags("Admin - Zones");

        // ---- Cameras (video sources) ----

        admin.MapGet("/cameras", async (Guid? zoneId, IFacilityAdminService service, CancellationToken ct) =>
            Results.Ok(await service.ListCamerasAsync(zoneId, ct))).WithTags("Admin - Cameras");

        admin.MapGet("/cameras/{id:guid}", async (Guid id, IFacilityAdminService service, CancellationToken ct) =>
            (await service.GetCameraAsync(id, ct)).ToHttpResult()).WithTags("Admin - Cameras");

        admin.MapPost("/cameras", async (CameraRequest request, IFacilityAdminService service, CancellationToken ct) =>
            (await service.CreateCameraAsync(request, ct))
                .ToHttpResult(camera => Results.Created($"/api/cameras/{camera.Id}", camera))).WithTags("Admin - Cameras");

        admin.MapPut("/cameras/{id:guid}", async (Guid id, CameraRequest request, IFacilityAdminService service, AppDbContext db, ProcessedFrameStore frames, IHubContext<ParkingHub> hub, CancellationToken ct) =>
        {
            var result=await service.UpdateCameraAsync(id,request,ct);
            if(result.Succeeded)await ObservationInvalidation.ClearAsync(id,db,frames,hub,ct);
            return result.ToHttpResult();
        }).WithTags("Admin - Cameras");

        admin.MapDelete("/cameras/{id:guid}", async (Guid id, IFacilityAdminService service, AppDbContext db, ProcessedFrameStore frames, IHubContext<ParkingHub> hub, CancellationToken ct) =>
        {
            var result=await service.DeleteCameraAsync(id,ct);
            if(result.Succeeded)await ObservationInvalidation.ClearAsync(id,db,frames,hub,ct);
            return result.ToHttpResult(_=>Results.NoContent());
        }).WithTags("Admin - Cameras");
    }
}
