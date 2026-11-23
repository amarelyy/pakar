namespace Pakar.AiInference.Video;

public sealed class WorkerOptions
{
    public bool Enabled { get; set; }
    public string ApiBaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string VideoRoot { get; set; } = "videos";
    public double SampleFps { get; set; } = 2;
    public double ConfigPollSeconds { get; set; } = 10;
    public double HeartbeatSeconds { get; set; } = 15;
    public double RetrySeconds { get; set; } = 2;

    public void Validate()
    {
        if (!Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || ApiKey.Length < 32)
            throw new ArgumentException("Worker requires HTTP(S) ApiBaseUrl and the shared ingest ApiKey (32+ characters).");
        if (!double.IsFinite(SampleFps) || SampleFps is < .1 or > 30 ||
            new[] { ConfigPollSeconds, HeartbeatSeconds, RetrySeconds }.Any(v => !double.IsFinite(v) || v < .1 || v > 120))
            throw new ArgumentException("Worker timing options are outside their supported ranges.");
    }

    public string ResolveVideo(string file)
    {
        if (string.IsNullOrWhiteSpace(file) || file.IndexOfAny(['/', '\\', ':']) >= 0 || file.Contains("..") ||
            !new[] { ".mp4", ".avi", ".mov", ".mkv", ".webm" }.Contains(Path.GetExtension(file).ToLowerInvariant()))
            throw new ArgumentException("Video must be a supported file name within VideoRoot.");
        return Path.Combine(Path.GetFullPath(VideoRoot), file);
    }
}
