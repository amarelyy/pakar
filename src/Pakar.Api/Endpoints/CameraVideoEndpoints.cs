using Pakar.Api.Security;
using Pakar.Application.Interfaces;
using Pakar.Domain.Services;

namespace Pakar.Api.Endpoints;

public static class CameraVideoEndpoints
{
    public static void MapCameraVideoEndpoints(this WebApplication app)
    {
        app.MapGet("/api/cameras/{id:guid}/video", GetVideoAsync)
            .RequireAuthorization(AuthPolicies.AdminOnly)
            .WithTags("Admin - Cameras");
    }

    private static async Task<IResult> GetVideoAsync(Guid id, IFacilityAdminService service,
        IConfiguration config, HttpResponse response, CancellationToken ct)
    {
        var result = await service.GetCameraAsync(id, ct);
        return result.ToHttpResult(camera =>
        {
            var file = camera.VideoFile;
            if (string.IsNullOrWhiteSpace(file) || FacilityRules.ValidateVideoFile(file) is not null)
                return Results.NotFound();

            var root = Path.GetFullPath(config["Video:Root"] ?? "videos");
            var path = Path.Combine(root, file);
            if (!File.Exists(path)) return Results.NotFound();

            var contentType = Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".mp4" => "video/mp4",
                ".webm" => "video/webm",
                ".mov" => "video/quicktime",
                ".avi" => "video/x-msvideo",
                _ => "video/x-matroska"
            };

            response.Headers.CacheControl = "private, no-store";
            return Results.File(path, contentType, enableRangeProcessing: true);
        });
    }
}
