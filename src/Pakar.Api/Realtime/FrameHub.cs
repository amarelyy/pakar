using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Pakar.Api.Security;
using Pakar.Shared.Dtos;

namespace Pakar.Api.Realtime;

/// <summary>
/// Live scanning channel. A browser camera sends JPEG frames (5-10 FPS) over WebSockets and gets back
/// only bounding-box JSON. The API forwards each frame to the inference service by HTTP POST.
/// Restricted to admins because it drives the camera simulation.
/// </summary>
[Authorize(Policy = AuthPolicies.AdminOnly)]
public sealed class FrameHub(InferenceClient inference) : Hub
{
    public const string Route = "/hubs/frames";

    /// <summary>Largest accepted JPEG; a compressed 720p frame is well below this.</summary>
    public const int MaxFrameBytes = 512 * 1024;

    public async Task<IReadOnlyList<BoundingBoxDto>> AnalyzeFrame(int cameraId, byte[] jpeg)
    {
        // 1. Reject empty, oversized or non-JPEG payloads (JPEG files start with FF D8).
        if (jpeg is null || jpeg.Length < 2)
            throw new HubException("Frame is empty.");
        if (jpeg.Length > MaxFrameBytes)
            throw new HubException($"Frame exceeds {MaxFrameBytes / 1024} KB.");
        if (jpeg[0] != 0xFF || jpeg[1] != 0xD8)
            throw new HubException("Frame must be a JPEG image.");

        // 2. Forward to the inference service; only client-safe messages leave the hub.
        try
        {
            return await inference.DetectAsync(cameraId, jpeg, Context.ConnectionAborted);
        }
        catch (InferenceUnavailableException ex)
        {
            throw new HubException(ex.Message);
        }
    }
}
