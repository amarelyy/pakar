using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Pakar.Api.Security;

namespace Pakar.Api.Middlewares;

/// <summary>
/// Authenticates edge devices posting to ingest routes. Callers must send the shared
/// key configured in <c>Ingest:ApiKey</c> in the <c>X-Api-Key</c> header.
/// </summary>
public sealed class CameraAuthMiddleware
{
    public const string HeaderName = "X-Api-Key";

    private readonly RequestDelegate _next;
    private readonly ILogger<CameraAuthMiddleware> _logger;
    private readonly byte[] _expectedHash;

    public CameraAuthMiddleware(RequestDelegate next, IOptions<IngestOptions> options, ILogger<CameraAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _expectedHash = Hash(options.Value.ApiKey);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var provided = context.Request.Headers[HeaderName].ToString();

        // Comparing fixed-size digests keeps the check constant-time regardless of key length.
        if (provided.Length == 0 || !CryptographicOperations.FixedTimeEquals(_expectedHash, Hash(provided)))
        {
            _logger.LogWarning("Rejected ingest request from {RemoteIp}: missing or invalid API key.",
                context.Connection.RemoteIpAddress);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await _next(context);
    }

    private static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
}
