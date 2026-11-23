using Pakar.Api.Security;
using Pakar.Application.DTOs;
using Pakar.Application.Interfaces;

namespace Pakar.Api.Endpoints;

/// <summary>Detection history: a public occupancy series per zone, and the raw records for admins.</summary>
public static class HistoryEndpoints
{
    public static void MapHistoryEndpoints(this WebApplication app)
    {
        var history = app.MapGroup("/api/history").WithTags("History");

        // Occupancy per hour or day, for charts and prediction. No per-slot or per-user data, so guests may read it.
        history.MapGet("/zones/{id:guid}", async (
                Guid id, DateTimeOffset? from, DateTimeOffset? to, string? bucket,
                IDetectionHistoryService service, CancellationToken ct) =>
            (await service.GetZoneSeriesAsync(id, new ZoneHistoryQuery(from, to, bucket), ct)).ToHttpResult())
            .AllowAnonymous();

        // Raw detection records, newest first.
        history.MapGet("/detections", async (
                Guid? zoneId, Guid? cameraId, int? spotId, string? status, DateTimeOffset? from, DateTimeOffset? to,
                int? page, int? pageSize, IDetectionHistoryService service, CancellationToken ct) =>
            (await service.ListRecordsAsync(new DetectionHistoryQuery(zoneId, cameraId, spotId, status, from, to, page, pageSize), ct)).ToHttpResult())
            .RequireAuthorization(AuthPolicies.AdminOnly);
    }
}
