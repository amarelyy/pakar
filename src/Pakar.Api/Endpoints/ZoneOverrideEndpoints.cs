using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Pakar.Api.Realtime;
using Pakar.Api.Security;
using Pakar.Application.DTOs;
using Pakar.Application.Interfaces;

namespace Pakar.Api.Endpoints;

/// <summary>
/// Manual zone status for admins (FR 17): force a zone to available, almost_full or full while its camera is down,
/// and clear it again. The change is stored, shown by every zone endpoint, and pushed to open maps through ParkingHub.
/// </summary>
public static class ZoneOverrideEndpoints
{
    public static void MapZoneOverrideEndpoints(this WebApplication app)
    {
        var zones = app.MapGroup("/api/zones").RequireAuthorization(AuthPolicies.AdminOnly).WithTags("Admin - Zone status override");

        zones.MapGet("/status-overrides", async (IZoneOverrideService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(ct)));

        zones.MapPut("/{id:guid}/status-override", async (
            Guid id, SetZoneOverrideRequest request, ClaimsPrincipal admin,
            IZoneOverrideService service, ParkingStateStore store, IHubContext<ParkingHub> hub, CancellationToken ct) =>
        {
            var result = await service.SetAsync(id, admin.GetUserId(), request, ct);
            if (result.Succeeded)
                await PushZoneAsync(store, hub, result.Value!.ZoneExternalId);
            return result.ToHttpResult();
        });

        zones.MapDelete("/{id:guid}/status-override", async (
            Guid id, IZoneOverrideService service, ParkingStateStore store, IHubContext<ParkingHub> hub, CancellationToken ct) =>
        {
            var result = await service.ClearAsync(id, ct);
            if (result.Succeeded)
                await PushZoneAsync(store, hub, result.Value!.ZoneExternalId);
            return result.Succeeded ? Results.NoContent() : result.ToHttpResult();
        });
    }

    // Same event the ingest route sends when a zone changes, so open maps update without reloading.
    private static async Task PushZoneAsync(ParkingStateStore store, IHubContext<ParkingHub> hub, int areaId) =>
        await hub.Clients.Group(ParkingHub.AllGroup).SendAsync("ZoneUpdated", store.RefreshZone(areaId));
}
