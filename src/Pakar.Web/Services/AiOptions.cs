namespace Pakar.Web.Services;

/// <summary>Settings of the browser-side detection, read from <c>Ai</c> in wwwroot/appsettings.json.</summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>ONNX slot model, relative to the site root or an absolute URL that allows cross-origin reads.</summary>
    public string ModelUrl { get; set; } = "models/best.onnx";

    /// <summary>Folder that holds the demo videos, relative to the site root or an absolute CORS-enabled URL.</summary>
    public string VideoBaseUrl { get; set; } = "videos/";

    /// <summary>Folder of the pinned onnxruntime-web build (script and wasm files).</summary>
    public string OrtBaseUrl { get; set; } = "https://cdn.jsdelivr.net/npm/onnxruntime-web@1.22.0/dist/";

    /// <summary>Frames analysed per second; the .NET worker uses 2.</summary>
    public double SampleFps { get; set; } = 2;

    /// <summary>Seconds between heartbeats while nothing changes, so the camera is not marked inactive (FR 12).</summary>
    public double HeartbeatSeconds { get; set; } = 15;

    public string VideoUrl(string file) =>
        VideoBaseUrl.TrimEnd('/') + "/" + Uri.EscapeDataString(file);
}
