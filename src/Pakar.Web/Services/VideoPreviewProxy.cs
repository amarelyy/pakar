using System.Net.Http.Headers;

namespace Pakar.Web.Services;

/// <summary>Same-origin media proxy; Pakar.Api validates the caller's JWT and Admin role on every request.</summary>
public static class VideoPreviewProxy
{
    public static void MapVideoPreviewProxy(this WebApplication app)
    {
        app.MapGet("/admin/video/{id:guid}", async (Guid id, HttpContext context, IHttpClientFactory clients) =>
        {
            var authorization = context.Request.Headers.Authorization.ToString();
            if (!AuthenticationHeaderValue.TryParse(authorization, out var header) ||
                !header.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase))
            { context.Response.StatusCode = 401; return; }
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/cameras/{id}/video");
            request.Headers.Authorization = header;
            try
            {
                using var response = await clients.CreateClient("api").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
                context.Response.StatusCode = (int)response.StatusCode;
                context.Response.Headers.CacheControl = "private, no-store";
                context.Response.Headers.XContentTypeOptions = "nosniff";
                if (!response.IsSuccessStatusCode) return;
                if (response.Content.Headers.ContentLength is > 50 * 1024 * 1024)
                { context.Response.StatusCode = 413; return; }
                context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
                await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
            }
            catch (HttpRequestException) { if(!context.Response.HasStarted) context.Response.StatusCode=502; }
            catch (TaskCanceledException) { if(!context.Response.HasStarted) context.Response.StatusCode=504; }
        });
    }
}
