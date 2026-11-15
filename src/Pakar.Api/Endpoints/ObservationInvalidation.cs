using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Pakar.Api.Realtime;
using Pakar.Application.DTOs;
using Pakar.Infrastructure.Persistence;

namespace Pakar.Api.Endpoints;

public static class ObservationInvalidation
{
    public static async Task ClearAsync(Guid id, AppDbContext db, ProcessedFrameStore frames, IHubContext<ParkingHub> hub, CancellationToken ct)
    {
        var externalId=await db.Cameras.IgnoreQueryFilters().Where(c=>c.Id==id).Select(c=>c.ExternalId).FirstOrDefaultAsync(ct);
        if(frames.Invalidate(externalId,DateTimeOffset.UtcNow) is {} invalid)
            await hub.Clients.Group(ParkingHub.AllGroup).SendAsync("ObservationsUpdated",invalid,ct);
    }
}
