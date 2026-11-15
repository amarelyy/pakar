using Microsoft.AspNetCore.SignalR;
using Pakar.Api.Realtime;
using Pakar.Application.DTOs;
using Pakar.Infrastructure.Persistence;
using Pakar.Api.Security;
using Pakar.Application.Interfaces;
using Pakar.Shared.Dtos;

namespace Pakar.Api.Endpoints;

public static class RoiEndpoints
{
    public static void MapRoiEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/api/cameras").RequireAuthorization(AuthPolicies.AdminOnly).WithTags("Admin - ROI");
        admin.MapGet("/{id:guid}/rois", async (Guid id, IRoiService service, CancellationToken ct) =>
            (await service.GetAsync(id, ct)).ToHttpResult());
        admin.MapPut("/{id:guid}/rois", async (Guid id, RoiRequest request, IRoiService service, AppDbContext db, ProcessedFrameStore frames, IHubContext<ParkingHub> hub, CancellationToken ct) =>
        {
            var result=await service.ReplaceAsync(id,request,ct);
            if(result.Succeeded)await ObservationInvalidation.ClearAsync(id,db,frames,hub,ct);
            return result.ToHttpResult();
        });
        app.MapGet("/api/internal/video-sources", async (IRoiService service, CancellationToken ct) =>
            Results.Ok(await service.GetSourcesAsync(ct))).WithTags("Worker configuration");
    }
}
