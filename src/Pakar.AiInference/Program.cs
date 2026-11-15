using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Pakar.AiInference.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<InferenceServiceOptions>()
    .Bind(builder.Configuration.GetSection(InferenceServiceOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IInferenceEngine, UnavailableInferenceEngine>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Called by Pakar.Api for every live frame: JPEG body in, bounding-box JSON out.
app.MapPost("/api/detect", async (HttpRequest request, IInferenceEngine engine,
    IOptions<InferenceServiceOptions> options, CancellationToken ct) =>
{
    // 1. Only Pakar.Api may call this service.
    var provided = request.Headers[InferenceServiceOptions.ApiKeyHeader].ToString();
    if (!KeysMatch(options.Value.ApiKey, provided))
        return Results.Unauthorized();

    // 2. Validate the payload before reading it into memory.
    if (request.ContentType?.StartsWith("image/jpeg", StringComparison.OrdinalIgnoreCase) != true)
        return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
    if (request.ContentLength is null or 0 || request.ContentLength > InferenceServiceOptions.MaxFrameBytes)
        return Results.BadRequest($"Frame must be between 1 byte and {InferenceServiceOptions.MaxFrameBytes / 1024} KB.");

    using var buffer = new MemoryStream((int)request.ContentLength);
    await request.Body.CopyToAsync(buffer, ct);

    // 3. Run detection.
    try
    {
        return Results.Ok(await engine.DetectAsync(buffer.ToArray(), ct));
    }
    catch (ModelNotLoadedException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.Run();

// Compares SHA-256 digests so the check is constant-time regardless of key length.
static bool KeysMatch(string expected, string provided) =>
    provided.Length > 0 && CryptographicOperations.FixedTimeEquals(
        SHA256.HashData(Encoding.UTF8.GetBytes(expected)),
        SHA256.HashData(Encoding.UTF8.GetBytes(provided)));

/// <summary>Settings bound from the "Inference" section; the key must match Pakar.Api's Inference:ApiKey.</summary>
public sealed class InferenceServiceOptions
{
    public const string SectionName = "Inference";
    public const string ApiKeyHeader = "X-Api-Key";
    public const int MaxFrameBytes = 512 * 1024;

    [Required]
    [MinLength(32)]
    public string ApiKey { get; init; } = string.Empty;
}
