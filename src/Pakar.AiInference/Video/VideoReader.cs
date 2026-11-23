using OpenCvSharp;

namespace Pakar.AiInference.Video;

public interface IVideoReader : IDisposable
{
    VideoFrame Read();
}
public interface IVideoReaderFactory
{
    IVideoReader Open(string path, double sampleFps);
}
public sealed record VideoFrame(Mat Image, bool Looped) : IDisposable
{
    public void Dispose() => Image.Dispose();
}
public sealed class OpenCvVideoReaderFactory : IVideoReaderFactory
{
    public IVideoReader Open(string path, double sampleFps) => new OpenCvVideoReader(path, sampleFps);
}

/// <summary>Recorded files only; frame indices sample the media independently of wall clock.</summary>
public sealed class OpenCvVideoReader : IVideoReader
{
    private readonly VideoCapture capture;
    private readonly double step, frameCount;
    private double nextFrame;
    public OpenCvVideoReader(string path, double sampleFps)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Video file is missing.", path);
        capture = new VideoCapture(path);
        double fps = capture.Fps;
        frameCount = capture.FrameCount;
        if (!capture.IsOpened() || !double.IsFinite(fps) || fps <= 0 || !double.IsFinite(frameCount) || frameCount < 1 || !double.IsFinite(sampleFps) || sampleFps <= 0)
        { capture.Dispose(); throw new InvalidDataException("Video cannot be decoded or has invalid timing metadata."); }
        step = Math.Max(1, fps / sampleFps);
    }
    public VideoFrame Read()
    {
        bool looped = nextFrame >= frameCount;
        if (looped) nextFrame = 0;
        capture.Set(VideoCaptureProperties.PosFrames, Math.Floor(nextFrame));
        nextFrame += step;
        var image = new Mat();
        if (!capture.Read(image) || image.Empty())
        { image.Dispose(); throw new InvalidDataException("Video frame cannot be decoded."); }
        return new(image, looped);
    }
    public void Dispose() => capture.Dispose();
}
