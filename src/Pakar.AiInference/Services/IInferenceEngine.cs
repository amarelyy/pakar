using Pakar.Shared.Dtos;

namespace Pakar.AiInference.Services;

/// <summary>Runs object detection on one JPEG frame.</summary>
public interface IInferenceEngine
{
    Task<IReadOnlyList<BoundingBoxDto>> DetectAsync(byte[] jpeg, CancellationToken ct);
}

/// <summary>Thrown when no model is loaded; the endpoint maps it to 503.</summary>
public sealed class ModelNotLoadedException(string message = "Detection model is not loaded.") : Exception(message);
