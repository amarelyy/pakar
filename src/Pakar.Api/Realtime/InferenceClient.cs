using System.Net.Http.Headers;
using System.Net.Http.Json;
using Pakar.Shared.Dtos;

namespace Pakar.Api.Realtime;

/// <summary>Connection settings for the AI inference service, bound from the "Inference" section.</summary>
public sealed class InferenceOptions
{
    public const string SectionName = "Inference";

    /// <summary>Base URL of Pakar.AiInference. Leave empty to run the API without live scanning.</summary>
    public string? BaseUrl { get; init; }

    /// <summary>Shared key sent in the X-Api-Key header; must match the inference service.</summary>
    public string? ApiKey { get; init; }

    public int TimeoutSeconds { get; init; } = 5;
}

/// <summary>Thrown when a frame cannot be analyzed; the message is safe to show to clients.</summary>
public sealed class InferenceUnavailableException(string message, Exception? inner = null)
    : Exception(message, inner);

/// <summary>Sends one JPEG frame to the inference service by HTTP POST and returns the detections.</summary>
public sealed class InferenceClient(HttpClient http, ILogger<InferenceClient> logger)
{
    public async Task<IReadOnlyList<BoundingBoxDto>> DetectAsync(int cameraId, byte[] jpeg, CancellationToken ct)
    {
        if (http.BaseAddress is null)
            throw new InferenceUnavailableException("Inference service is not configured.");

        using var content = new ByteArrayContent(jpeg);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");

        try
        {
            using var response = await http.PostAsync($"api/detect?cameraId={cameraId}", content, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Inference service returned {Status} for camera {Cam}.", (int)response.StatusCode, cameraId);
                throw new InferenceUnavailableException("Inference service could not analyze the frame.");
            }

            return await response.Content.ReadFromJsonAsync<List<BoundingBoxDto>>(ct) ?? [];
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            // Connection failures and timeouts both mean the service is unreachable right now.
            logger.LogWarning(ex, "Inference service unreachable for camera {Cam}.", cameraId);
            throw new InferenceUnavailableException("Inference service is unreachable.", ex);
        }
    }
}
