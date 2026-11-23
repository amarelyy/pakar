using Pakar.AiInference.Video;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Pakar.Shared.Dtos;

namespace Pakar.AiInference.Tests;

public class VideoTests
{
    [Theory]
    [InlineData("../clip.mp4")]
    [InlineData("C:\\clip.mp4")]
    [InlineData("rtsp://camera")]
    [InlineData("clip.txt")]
    public void RejectsPathsAndUnsupportedFiles(string file) => Assert.Throws<ArgumentException>(() => new WorkerOptions().ResolveVideo(file));
    [Fact] public void MissingVideoIsExplicit() => Assert.Throws<FileNotFoundException>(() => new OpenCvVideoReader("missing-video.mp4", 2));
    [Fact] public void InvalidTimingRejected() => Assert.Throws<ArgumentException>(() => new WorkerOptions { ApiBaseUrl = "http://localhost", ApiKey = new('x', 32), SampleFps = double.NaN }.Validate());
    [Fact]
    public async Task CancellationStopsRetryDelay()
    {
        var worker = new VideoSourceWorker(null!, null!, new OpenCvVideoReaderFactory(),
            Options.Create(new WorkerOptions { VideoRoot = Path.GetTempPath(), RetrySeconds = 10 }),
            TimeProvider.System, NullLogger<VideoSourceWorker>.Instance);
        WorkerSourceDto source = new(1, 1, "test", "nonexistent-test-video.mp4",
            [new(1, "A", [[.1, .1], [.4, .1], [.4, .4], [.1, .4]])], 1);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        using var client = new HttpClient();
        await worker.RunSourceAsync(source, null!, client, cancellation.Token).WaitAsync(TimeSpan.FromSeconds(3));
    }
}
