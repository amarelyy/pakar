using System.Net;
using System.Text.Json;
using Pakar.Application.DTOs;

namespace Pakar.Web.Services;

/// <summary>Outcome of an auth call; <see cref="Error"/> is safe to show to the user.</summary>
public sealed record AuthResult(bool Succeeded, string? Error = null, SignedInUser? User = null);

/// <summary>Calls Pakar.Api account endpoints: registration (FR 7) and login (FR 8).</summary>
public sealed class AuthApiClient(IHttpClientFactory httpClientFactory, ILogger<AuthApiClient> logger)
{
    private const string UnreachableMessage = "Cannot reach the server. Please try again.";

    public Task<AuthResult> RegisterAsync(string name, string email, string password, CancellationToken ct = default)
        => PostAsync("api/auth/register", new { name, email, password }, ct);

    public Task<AuthResult> LoginAsync(string email, string password, CancellationToken ct = default)
        => PostAsync(LoginPath, new { email, password }, ct);

    private const string LoginPath = "api/auth/login";

    private async Task<AuthResult> PostAsync(string path, object body, CancellationToken ct)
    {
        try
        {
            using var response = await httpClientFactory.CreateClient("api").PostAsJsonAsync(path, body, ct);
            if (!response.IsSuccessStatusCode)
                return new AuthResult(false, await ReadErrorAsync(response, ct));

            // Registration returns only a message; login returns the token and the account.
            if (path != LoginPath)
                return new AuthResult(true);

            var login = await response.Content.ReadFromJsonAsync<LoginResponseDto>(ct);
            if (login is null || string.IsNullOrEmpty(login.Token))
                return new AuthResult(false, "Unexpected response from the server. Please try again.");

            return new AuthResult(true, User: new SignedInUser(
                login.Token, login.ExpiresAt, login.User.Id, login.User.Name, login.User.Email, login.User.Role));
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            logger.LogWarning(ex, "Auth request to {Path} failed.", path);
            return new AuthResult(false, UnreachableMessage);
        }
    }

    /// <summary>Turns an API error response into one readable sentence.</summary>
    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return "Invalid email or password.";
        if (response.StatusCode == HttpStatusCode.Forbidden)
            return "This account has been deactivated. Contact an administrator.";
        if ((int)response.StatusCode >= 500)
            return "Something went wrong on the server. Please try again.";

        // 400 responses are either a plain-text message ("Email already registered.") or validation problem details.
        var body = await response.Content.ReadAsStringAsync(ct);
        if (response.Content.Headers.ContentType?.MediaType == "text/plain" && !string.IsNullOrWhiteSpace(body))
            return body.Trim();

        try
        {
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            if (root.ValueKind == JsonValueKind.String)
                return root.GetString()!;
            if (root.TryGetProperty("errors", out var errors))
            {
                foreach (var field in errors.EnumerateObject())
                    foreach (var message in field.Value.EnumerateArray())
                        return message.GetString()!;
            }
        }
        catch (JsonException)
        {
            // Fall through to the generic message.
        }

        return "The request was rejected. Please check your input.";
    }
}
