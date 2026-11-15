using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Pakar.Web.Services;

public sealed class ApiException(string message, IReadOnlyDictionary<string, string[]>? errors = null) : Exception(message)
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors ?? new Dictionary<string, string[]>();
}

/// <summary>Uses the current circuit's token per request, never shared default headers.</summary>
public sealed class ParkingApiClient(IHttpClientFactory factory, UserSession session)
{
    public async Task<T> Get<T>(string path, bool authenticated = false, CancellationToken ct = default)
    {
        using var response = await Send(HttpMethod.Get, path, null, authenticated, ct);
        try { return await response.Content.ReadFromJsonAsync<T>(ct) ?? throw new ApiException("Respons server kosong. Coba lagi."); }
        catch (JsonException) { throw new ApiException("Respons server tidak dapat dibaca. Coba lagi."); }
    }

    public async Task Write(HttpMethod method, string path, object? body = null, CancellationToken ct = default)
    {
        using var response = await Send(method, path, body, true, ct);
    }

    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, object? body, bool authenticated, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path);
        if (authenticated)
        {
            var token = await session.GetTokenAsync();
            if (token is null) throw new ApiException("Sesi berakhir. Silakan masuk kembali.");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        if (body is not null) request.Content = JsonContent.Create(body);
        HttpResponseMessage response;
        try { response = await factory.CreateClient("api").SendAsync(request, ct); }
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        { throw new ApiException("Server belum dapat dihubungi. Periksa koneksi lalu coba lagi."); }
        if (response.IsSuccessStatusCode) return response;
        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized) throw new ApiException("Sesi berakhir. Silakan masuk kembali.");
            if (response.StatusCode == HttpStatusCode.Forbidden) throw new ApiException("Akun ini tidak memiliki akses untuk tindakan tersebut.");
            if ((int)response.StatusCode >= 500) throw new ApiException("Server mengalami masalah. Coba lagi sebentar.");
            try
            {
                var problem = await response.Content.ReadFromJsonAsync<ApiProblem>(ct);
                throw new ApiException(problem?.Detail ?? problem?.Title ?? "Permintaan ditolak. Periksa isian Anda.", problem?.Errors);
            }
            catch (JsonException) { throw new ApiException("Permintaan ditolak. Muat ulang data dan coba lagi."); }
        }
    }
    private sealed record ApiProblem(string? Title, string? Detail, Dictionary<string, string[]>? Errors);
}
