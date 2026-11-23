using Pakar.Api.Realtime;
using Pakar.Api.Security;
using Pakar.Application.Interfaces;

namespace Pakar.Api.Endpoints;

/// <summary>
/// Routes for the browser feeder page (admin only): the page runs the detection model in the browser, so it
/// reads the video sources and sends results with the admin's JWT instead of the shared ingest API key,
/// which must never be shipped to a browser.
/// </summary>
public static class FeederEndpoints
{
    public static void MapFeederEndpoints(this WebApplication app)
    {
        var feeder = app.MapGroup("/api/feeder").RequireAuthorization(AuthPolicies.AdminOnly).WithTags("Admin - Browser feeder");

        // Same payload as GET /api/internal/video-sources: video file, slot polygons and zone capacity.
        feeder.MapGet("/sources", async (IRoiService service, CancellationToken ct) =>
            Results.Ok(await service.GetSourcesAsync(ct)));

        // Same payload and effect as POST /api/ingest/detections.
        feeder.MapPost("/detections", IngestEndpoints.IngestAsync);
    }
}
