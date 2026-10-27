using Pakar.Shared.Dtos;

namespace Pakar.AiInference.Services;

/// <summary>Runs object detection on one JPEG frame.</summary>
public interface IInferenceEngine
{
    Task<IReadOnlyList<BoundingBoxDto>> DetectAsync(byte[] jpeg, CancellationToken ct);
}

/// <summary>Thrown when no model is loaded; the endpoint maps it to 503.</summary>
public sealed class ModelNotLoadedException() : Exception("Detection model is not loaded.");

/// <summary>
/// Placeholder until the ONNX Runtime engine is implemented (see TODO.md). Keeps the HTTP contract
/// testable end to end while reporting that no model is available.
/// </summary>
public sealed class UnavailableInferenceEngine : IInferenceEngine
{
    public Task<IReadOnlyList<BoundingBoxDto>> DetectAsync(byte[] jpeg, CancellationToken ct)
        => throw new ModelNotLoadedException();
}
