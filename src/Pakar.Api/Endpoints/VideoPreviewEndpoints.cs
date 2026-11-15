using Pakar.Api.Security;
using Pakar.Application.Interfaces;
using Pakar.Domain.Services;

namespace Pakar.Api.Endpoints;

/// <summary>Private dummy-video preview. Files are outside wwwroot and every read requires an Admin JWT.</summary>
public static class VideoPreviewEndpoints
{
    public const long MaxPreviewBytes = 50 * 1024 * 1024;

    public static void MapVideoPreviewEndpoints(this WebApplication app)
    {
        app.MapGet("/api/cameras/{id:guid}/video", async (Guid id, IFacilityAdminService facilities,
            IConfiguration config, HttpContext context, CancellationToken ct) =>
        {
            var source = await facilities.GetCameraAsync(id, ct);
            if (!source.Succeeded) return source.ToHttpResult();
            var file = source.Value!.VideoFile;
            if (string.IsNullOrWhiteSpace(file) || FacilityRules.ValidateVideoFile(file) is not null)
                return Results.Problem("This source has no preview video configured.", statusCode: 404);
            var configuredRoot = config["Video:Root"];
            if (string.IsNullOrWhiteSpace(configuredRoot))
                return Results.Problem("Video preview is not configured on this server.", statusCode: 503);
            var root = Path.GetFullPath(configuredRoot);
            var path = Path.GetFullPath(Path.Combine(root, file));
            if (!path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                return Results.BadRequest();
            var info = new FileInfo(path);
            if (!info.Exists || info.LinkTarget is not null) return Results.NotFound();
            if (info.Length > MaxPreviewBytes)
                return Results.Problem("Preview supports clips up to 50 MB.", statusCode: 413);
            var type = Path.GetExtension(file).ToLowerInvariant() switch
            {
                ".mp4" => "video/mp4", ".webm" => "video/webm", ".mov" => "video/quicktime",
                ".avi" => "video/x-msvideo", _ => "video/x-matroska"
            };
            context.Response.Headers.CacheControl = "private, no-store";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            return Results.File(path, type, enableRangeProcessing: true);
        }).RequireAuthorization(AuthPolicies.AdminOnly).WithTags("Admin - Cameras");
    }
}
