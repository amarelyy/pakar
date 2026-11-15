using OpenCvSharp;

namespace Pakar.AiInference.Video;

/// <summary>Conservative rejection of nearly black or featureless frames, not night/rain certification.</summary>
public static class FrameQuality
{
    public static string? RejectionReason(Mat image)
    {
        if(image.Empty()) return "invalid_frame";
        using var small=new Mat();using var gray=new Mat();
        Cv2.Resize(image,small,new Size(160,90));Cv2.CvtColor(small,gray,ColorConversionCodes.BGR2GRAY);
        Cv2.MeanStdDev(gray,out var mean,out var deviation);
        if(mean.Val0<20)return "frame_too_dark";
        return deviation.Val0<5 ? "frame_low_contrast" : null;
    }
}
