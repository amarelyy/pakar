using OpenCvSharp;
using Pakar.AiInference.Video;

namespace Pakar.AiInference.Tests;

public sealed class FrameQualityTests
{
    [Fact] public void BlackAndUniformBrightFramesAreNotParkingEvidence()
    {
        using var black=new Mat(90,160,MatType.CV_8UC3,Scalar.All(0));
        using var white=new Mat(90,160,MatType.CV_8UC3,Scalar.All(255));
        Assert.Equal("frame_too_dark",FrameQuality.RejectionReason(black));
        Assert.Equal("frame_low_contrast",FrameQuality.RejectionReason(white));
    }
    [Fact] public void TexturedDaylightFrameIsNotRejected()
    {
        using var image=new Mat(90,160,MatType.CV_8UC3,Scalar.All(70));
        Cv2.Rectangle(image,new Rect(0,0,80,90),Scalar.All(180),-1);
        Assert.Null(FrameQuality.RejectionReason(image));
    }
}
