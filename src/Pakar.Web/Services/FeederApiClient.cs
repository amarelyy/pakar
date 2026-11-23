using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Pakar.Shared.Dtos;

namespace Pakar.Web.Services;

/// <summary>Admin-only calls used by the browser feeder: read the video sources and post detection results.</summary>
public sealed class FeederApiClient(IHttpClientFactory clients, UserSession session)
{
    public async Task<List<WorkerSourceDto>> GetSourcesAsync(CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Get, "api/feeder/sources", null, ct);
        return await response.Content.ReadFromJsonAsync<List<WorkerSourceDto>>(ct) ?? [];
    }

    public async Task PostDetectionsAsync(object payload, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Post, "api/feeder/detections", JsonContent.Create(payload), ct);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, HttpContent? content, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await session.GetTokenAsync());
        var response = await clients.CreateClient("api").SendAsync(request, ct);
        if (response.IsSuccessStatusCode) return response;

        using (response)
        {
            var message = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Your session expired. Please sign in again.",
                HttpStatusCode.Forbidden => "Administrator access is required.",
                HttpStatusCode.NotFound => "The camera of this source is not registered in the API.",
                _ => "The API could not complete this request."
            };
            throw new HttpRequestException(message, null, response.StatusCode);
        }
    }
}
