using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using Pakar.Shared.Dtos;

namespace Pakar.Web.Services;

/// <summary>A video file picked on this computer, played from a temporary browser address.</summary>
public sealed record LocalVideo(string Url, string Name, long Size);

/// <summary>One analysed frame: boxes normalized to the frame, its size and timing.</summary>
public sealed record FrameDetection(IReadOnlyList<BoundingBoxDto> Boxes, int Width, int Height, double VideoTime, double Milliseconds);

/// <summary>
/// Runs the slot model in the browser through <c>wwwroot/js/ai-feeder.js</c> (ONNX Runtime Web).
/// JavaScript only executes the model; mapping to slots and all rules are C#.
/// </summary>
public sealed class BrowserDetector(IJSRuntime js, IOptions<AiOptions> options) : IAsyncDisposable
{
    private IJSObjectReference? module;

    /// <summary>Execution provider in use ("webgpu" or "wasm"); set after <see cref="LoadAsync"/>.</summary>
    public string? Provider { get; private set; }

    private async Task<IJSObjectReference> ModuleAsync(CancellationToken ct = default) =>
        module ??= await js.InvokeAsync<IJSObjectReference>("import", ct, "./js/ai-feeder.js");

    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (Provider is not null) return;
        var module = await ModuleAsync(ct);
        Provider = await module.InvokeAsync<string>("load", ct, options.Value.OrtBaseUrl, options.Value.ModelUrl);
    }

    /// <summary>Analyses the current frame of the video element; null while the video has no frame yet.</summary>
    public async Task<FrameDetection?> DetectAsync(ElementReference video, CancellationToken ct = default)
    {
        if (module is null) throw new InvalidOperationException("Load the model first.");
        var result = await module.InvokeAsync<JsDetection?>("detect", ct, video);
        if (result is null) return null;
        var boxes = result.Boxes.Select(b => new BoundingBoxDto(b.X, b.Y, b.Width, b.Height, b.Confidence, b.Label)).ToList();
        return new FrameDetection(boxes, result.Width, result.Height, result.VideoTime, result.Milliseconds);
    }

    public async Task<bool> PlayAsync(ElementReference video) =>
        module is not null && await module.InvokeAsync<bool>("play", video);

    public async Task PauseAsync(ElementReference video)
    {
        if (module is not null) await module.InvokeVoidAsync("pause", video);
    }

    /// <summary>Uses the video file chosen in the given file input; null when the selection was cleared.</summary>
    public async Task<LocalVideo?> UseLocalVideoAsync(ElementReference input) =>
        await (await ModuleAsync()).InvokeAsync<LocalVideo?>("useLocalVideo", input);

    public async Task ReleaseLocalVideoAsync()
    {
        if (module is not null) await module.InvokeVoidAsync("releaseLocalVideo");
    }

    public async ValueTask DisposeAsync()
    {
        if (module is not null) await module.DisposeAsync();
    }

    private sealed record JsBox(float X, float Y, float Width, float Height, float Confidence, string Label);
    private sealed record JsDetection(List<JsBox> Boxes, int Width, int Height, double VideoTime, double Milliseconds);
}
