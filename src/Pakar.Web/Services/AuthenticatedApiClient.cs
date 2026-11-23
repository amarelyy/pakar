using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Pakar.Web.Services;

public sealed record ApiError(string Message, IReadOnlyDictionary<string, string[]> Fields);

public class ApiResult
{
    public ApiError? Error { get; init; }
    public bool Ok => Error is null;
}

public sealed class ApiResult<T> : ApiResult
{
    public T? Value { get; init; }
}

/// <summary>Calls Pakar.Api with the signed-in user's bearer token.</summary>
public sealed class AuthenticatedApiClient(IHttpClientFactory factory, UserSession session)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly IReadOnlyDictionary<string, string[]> NoFields =
        new Dictionary<string, string[]>();

    public Task<ApiResult<T>> GetAsync<T>(string url) => SendAsync<T>(HttpMethod.Get, url);

    public async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string url, object? body = null)
    {
        var (response, error) = await SendCoreAsync(method, url, body);
        using (response)
        {
            if (error is not null)
                return new ApiResult<T> { Error = error };

            try
            {
                var value = await response!.Content.ReadFromJsonAsync<T>(Json);
                if (value is not null)
                    return new ApiResult<T> { Value = value };
            }
            catch (Exception ex) when (ex is JsonException or HttpRequestException or OperationCanceledException)
            {
                // A response with an unreadable body is reported through the same UI error path.
            }

            return new ApiResult<T> { Error = new ApiError("The server response could not be read.", NoFields) };
        }
    }

    /// <summary>POST, PUT or DELETE. The response body is ignored; callers reload the data.</summary>
    public async Task<ApiResult> SendAsync(HttpMethod method, string url, object? body = null)
    {
        var (response, error) = await SendCoreAsync(method, url, body);
        using (response) return new ApiResult { Error = error };
    }

    private async Task<(HttpResponseMessage? Response, ApiError? Error)> SendCoreAsync(
        HttpMethod method, string url, object? body)
    {
        var token = await session.GetTokenAsync();
        if (token is null)
            return (null, new ApiError("Your session has expired. Please log in again.", NoFields));

        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body, options: Json);

        HttpResponseMessage response;
        try
        {
            response = await factory.CreateClient("api").SendAsync(request);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return (null, new ApiError("Could not reach the server. Is Pakar.Api running?", NoFields));
        }

        if (response.IsSuccessStatusCode) return (response, null);

        var error = await ReadErrorAsync(response);
        response.Dispose();
        return (null, error);
    }

    /// <summary>Turns a 400/401/403/404/409 response into a message plus per-field errors (RFC 7807).</summary>
    private static async Task<ApiError> ReadErrorAsync(HttpResponseMessage response)
    {
        var fallback = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Your session has expired. Please log in again.",
            HttpStatusCode.Forbidden => "You do not have permission to do this.",
            HttpStatusCode.NotFound => "Not found. It may have been removed.",
            HttpStatusCode.Conflict => "This conflicts with the current data.",
            _ => $"The server returned an error ({(int)response.StatusCode})."
        };

        try
        {
            var text = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(text)) return new ApiError(fallback, NoFields);

            if (!text.TrimStart().StartsWith('{'))
                return new ApiError(text.Trim().Trim('"'), NoFields); // plain-text body, e.g. BadRequest("...")

            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            var fields = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in errors.EnumerateObject())
                {
                    fields[property.Name] = property.Value.ValueKind == JsonValueKind.Array
                        ? property.Value.EnumerateArray().Select(e => e.GetString() ?? "").ToArray()
                        : [property.Value.GetString() ?? ""];
                }
            }

            string? message = null;
            if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                message = detail.GetString();
            else if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                message = title.GetString();

            return new ApiError(message ?? fallback, fields);
        }
        catch (JsonException)
        {
            return new ApiError(fallback, NoFields);
        }
    }
}