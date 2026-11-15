using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Pakar.Application.DTOs;
using Pakar.Shared.Dtos;

namespace Pakar.Web.Services;

public sealed class RoiApiClient(IHttpClientFactory clients, UserSession session)
{
    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, HttpContent? content = null, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await session.GetTokenAsync());
        var response = await clients.CreateClient("api").SendAsync(request, ct);
        if (response.IsSuccessStatusCode) return response;
        using (response)
        {
            string message = response.StatusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized => "Your session expired. Please sign in again.",
                System.Net.HttpStatusCode.Forbidden => "Administrator access is required.",
                System.Net.HttpStatusCode.NotFound => "Source or reference frame was not found. Reload the source list.",
                System.Net.HttpStatusCode.Conflict => "The source changed. Reload before saving again.",
                _ => "The API could not complete this request."
            };
            var body = await response.Content.ReadAsStringAsync(ct);
            try
            {
                using var json = JsonDocument.Parse(body);
                if (json.RootElement.TryGetProperty("errors", out var errors))
                    message = string.Join(" ", errors.EnumerateObject().SelectMany(p => p.Value.EnumerateArray().Select(v => p.Name + ": " + v.GetString())));
                else if (json.RootElement.ValueKind == JsonValueKind.String) message = json.RootElement.GetString()!;
            }
            catch (JsonException) { }
            throw new HttpRequestException(message, null, response.StatusCode);
        }
    }
    public async Task<List<CameraAdminDto>> Cameras(CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Get, "api/cameras", ct: ct);
        return (await response.Content.ReadFromJsonAsync<List<CameraAdminDto>>(ct))!;
    }
    public async Task<CameraRoisDto> Get(Guid id, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Get, $"api/cameras/{id}/rois", ct: ct);
        return (await response.Content.ReadFromJsonAsync<CameraRoisDto>(ct))!;
    }
    public async Task<CameraRoisDto> Save(Guid id, RoiRequest value, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Put, $"api/cameras/{id}/rois", JsonContent.Create(value), ct);
        return (await response.Content.ReadFromJsonAsync<CameraRoisDto>(ct))!;
    }
    public async Task<(byte[] Bytes, string ContentType)> Frame(Guid id, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Get, $"api/cameras/{id}/reference-frame", ct: ct);
        return (await response.Content.ReadAsByteArrayAsync(ct), response.Content.Headers.ContentType!.MediaType!);
    }
    public async Task Upload(Guid id, byte[] bytes, string name, CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(bytes), "file", name);
        using var response = await SendAsync(HttpMethod.Put, $"api/cameras/{id}/reference-frame", content, ct);
    }
}
