using Microsoft.EntityFrameworkCore;
using Pakar.Api.Security;
using Pakar.Application.Validation;
using Pakar.Domain.Entities;
using Pakar.Infrastructure.Persistence;

namespace Pakar.Api.Endpoints;

public static class ReferenceFrameEndpoints
{
    public static void MapReferenceFrameEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/api/cameras").RequireAuthorization(AuthPolicies.AdminOnly).WithTags("Admin - ROI");
        admin.MapGet("/{id:guid}/reference-frame", async (Guid id, AppDbContext db, HttpResponse response, CancellationToken ct) =>
        {
            var frame = await db.CameraFrames.AsNoTracking().FirstOrDefaultAsync(f => f.CameraId == id, ct);
            response.Headers.CacheControl = "no-store";
            return frame is null ? Results.NotFound() : Results.File(frame.Data, frame.ContentType);
        });
        // Bearer-token admin auth, no ambient cookie credentials. Multipart is read manually.
        admin.MapPut("/{id:guid}/reference-frame", async (Guid id, HttpRequest request, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            if (!await db.Cameras.AnyAsync(c => c.Id == id, ct)) return Results.NotFound();
            if (!request.HasFormContentType || request.ContentLength > ReferenceFrameRules.MaxBytes + 65536)
                return Results.BadRequest("Upload a PNG or JPEG up to 2 MB in multipart field 'file'.");
            try
            {
                var form = await request.ReadFormAsync(ct);
                var file = form.Files.GetFile("file");
                if (file is null || file.Length is <= 0 or > ReferenceFrameRules.MaxBytes)
                    return Results.BadRequest("File must be between 1 byte and 2 MB.");
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream, ct);
                var bytes = stream.ToArray();
                var contentType = ReferenceFrameRules.ContentType(bytes);
                if (contentType is null) return Results.BadRequest("File signature must be PNG or JPEG.");
                var frame = await db.CameraFrames.FirstOrDefaultAsync(f => f.CameraId == id, ct);
                if (frame is null) { frame = new CameraFrame { CameraId = id }; db.CameraFrames.Add(frame); }
                frame.Data = bytes; frame.ContentType = contentType; frame.UpdatedAt = clock.GetUtcNow().UtcDateTime;
                await db.SaveChangesAsync(ct);
                return Results.Ok(new { frame.UpdatedAt, frame.ContentType });
            }
            catch (InvalidDataException) { return Results.BadRequest("Invalid multipart upload or file is too large."); }
            catch (BadHttpRequestException) { return Results.BadRequest("Invalid upload or file is too large."); }
            catch (DbUpdateException) { return Results.Conflict("Source changed concurrently. Reload and retry."); }
        });
    }
}
