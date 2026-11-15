using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Pakar.AiInference.Detection;
using Pakar.AiInference.Services;
using Pakar.Shared.Dtos;

namespace Pakar.AiInference.Video;

/// <summary>One task per configured source; configuration is refreshed without database credentials.</summary>
public sealed class VideoSourceWorker(IServiceProvider services, IHttpClientFactory clients,
    IVideoReaderFactory readers, IOptions<WorkerOptions> configured, TimeProvider clock, ILogger<VideoSourceWorker> log) : BackgroundService
{
    private sealed record Running(string Fingerprint, CancellationTokenSource Stop, Task Task);
    private readonly WorkerOptions options = configured.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled) return;
        options.Validate();
        var engine = services.GetRequiredService<OnnxInferenceEngine>();
        using var client = clients.CreateClient("parking-worker");
        var running = new Dictionary<int, Running>();
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var sources = await client.GetFromJsonAsync<List<WorkerSourceDto>>("api/internal/video-sources", stoppingToken)
                        ?? throw new InvalidDataException("Worker configuration must be an array.");
                    if (sources.Select(s => s.CameraId).Distinct().Count() != sources.Count) throw new InvalidDataException("Duplicate camera IDs.");
                    var desired = sources.ToDictionary(s => s.CameraId, s => (Source: s, Fingerprint: JsonSerializer.Serialize(s)));
                    foreach (var id in running.Keys.ToArray())
                    {
                        if (desired.TryGetValue(id, out var wanted) && wanted.Fingerprint == running[id].Fingerprint && !running[id].Task.IsCompleted) continue;
                        var old = running[id]; old.Stop.Cancel();
                        await old.Task; old.Stop.Dispose(); running.Remove(id);
                    }
                    foreach (var (id, wanted) in desired)
                    {
                        if (running.ContainsKey(id)) continue;
                        var stop = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                        // Video decoding and ORT are synchronous; keep them off the config-poll loop.
                        var task = Task.Run(() => RunSourceAsync(wanted.Source, engine, client, stop.Token), CancellationToken.None);
                        running[id] = new(wanted.Fingerprint, stop, task);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { log.LogWarning("Worker config refresh failed ({Error}); existing sources continue.", ex.GetType().Name); }
                await Task.Delay(TimeSpan.FromSeconds(options.ConfigPollSeconds), clock, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            foreach (var item in running.Values) item.Stop.Cancel();
            await Task.WhenAll(running.Values.Select(r => r.Task));
            foreach (var item in running.Values) item.Stop.Dispose();
        }
    }

    public async Task RunSourceAsync(WorkerSourceDto source, OnnxInferenceEngine engine, HttpClient client, CancellationToken ct)
    {
        try
        {
            if (source.CameraId <= 0 || source.AreaId <= 0) throw new ArgumentException("External IDs must be positive.");
            string path = options.ResolveVideo(source.VideoFile);
            using var mapper = new RoiMapper(source.Slots);
            var tracker = new OccupancyTracker(source.Slots);
            long started = clock.GetTimestamp(); double lastFrame = double.NegativeInfinity, lastSent = double.NegativeInfinity;
            bool sync = true; int failures = 0;
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using var reader = readers.Open(path, options.SampleFps);
                    while (!ct.IsCancellationRequested)
                    {
                        long tick = clock.GetTimestamp();
                        using var frame = reader.Read();
                        double seconds = clock.GetElapsedTime(started, tick).TotalSeconds;
                        if (frame.Looped || seconds - lastFrame > Math.Max(2, 2 / options.SampleFps))
                        { tracker.ResetPending(); sync = true; }
                        lastFrame = seconds;
                        if (frame.Looped) log.LogInformation("Video loop restarted camera={Camera}.", source.CameraId);
                        var detections = engine.Detect(frame.Image, ct);
                        var result = tracker.Update(mapper.Map(detections, frame.Image.Width, frame.Image.Height), seconds, source.ZoneCapacity);
                        if (sync || result.Changes.Count > 0 || seconds - lastSent >= options.HeartbeatSeconds)
                        {
                            // UTC is the send/observation time; monotonic elapsed seconds drive debounce.
                            var now = clock.GetUtcNow();
                            string kind = result.Changes.Count > 0 ? "change" : "heartbeat";
                            // A healthy frame with all slots undecided legitimately sends an empty heartbeat.
                            var spots = result.Visible.Select(s => new { s.SpotId, s.SpotNumber, source.AreaId, source.CameraId, s.Status, s.Confidence, timestamp = now }).ToList();
                            var payload = new { source.CameraId, source.AreaId, type = kind, timestamp = now, spots };
                            using var response = await client.PostAsJsonAsync("api/ingest/detections", payload, ct);
                            response.EnsureSuccessStatusCode();
                            lastSent = seconds; sync = false; failures = 0;
                            log.LogInformation("Ingest accepted camera={Camera} type={Type} slots={Slots} undecided={Unknown} pending={Pending}.",
                                source.CameraId, kind, spots.Count, result.Unknown.Count, result.Pending);
                        }
                        var remaining = TimeSpan.FromSeconds(1 / options.SampleFps) - clock.GetElapsedTime(tick);
                        if (remaining > TimeSpan.Zero) await Task.Delay(remaining, clock, ct);
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    tracker.ResetPending(); sync = true;
                    double delay = Math.Min(30, options.RetrySeconds * Math.Pow(2, Math.Min(failures++, 4)));
                    log.LogWarning("Source interrupted camera={Camera} error={Error}; retry in {Delay}s. No health heartbeat sent for invalid frames.", source.CameraId, ex.GetType().Name, delay);
                    await Task.Delay(TimeSpan.FromSeconds(delay), clock, ct);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) { log.LogError("Source configuration rejected camera={Camera} error={Error}.", source.CameraId, ex.Message); }
    }
}
