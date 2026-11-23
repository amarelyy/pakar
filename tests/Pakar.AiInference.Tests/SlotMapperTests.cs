using Pakar.AiInference.Detection;
using Pakar.Shared.Dtos;
using Pakar.Vision;

namespace Pakar.AiInference.Tests;

/// <summary>The managed mapper used in the browser must agree with the OpenCV mapper used by the worker.</summary>
public class SlotMapperTests
{
    private const int Width = 1280, Height = 720;

    private static double[][] Rect(double x1, double y1, double x2, double y2) => [[x1, y1], [x2, y1], [x2, y2], [x1, y2]];

    private static List<WorkerSlotDto> Slots() =>
    [
        new(1, "A", Rect(.10, .10, .40, .40)),
        new(2, "B", Rect(.60, .10, .90, .40)),
        new(3, "C", [[.10, .60], [.40, .60], [.25, .90]])
    ];

    [Fact]
    public void ExactOverlapOfRectangleAndPolygon()
    {
        // The box covers half of the unit square: intersection 0.5, union 1 + 0.5 - 0.5 = 1, so IoU = 0.5.
        Assert.Equal(0.5, PolygonOverlap.Iou(Rect(0, 0, 1, 1), 0, 0, 1, .5), 9);
        Assert.Equal(1.0, PolygonOverlap.Iou(Rect(.2, .2, .6, .6), .2, .2, .6, .6), 9);
        Assert.Equal(0.0, PolygonOverlap.Iou(Rect(0, 0, .2, .2), .5, .5, .9, .9), 9);
        Assert.Equal(.5, PolygonOverlap.IntersectionArea(Rect(0, 0, 1, 1), 0, 0, 1, .5), 9);
    }

    [Fact]
    public void TriangleIsClippedByTheBox()
    {
        // Triangle (0,0) (1,0) (0,1) has area 0.5; the lower-left unit square quarter keeps 0.5 - 0.125 = 0.375 of its area inside x <= 0.5.
        double[][] triangle = [[0, 0], [1, 0], [0, 1]];
        Assert.Equal(0.375, PolygonOverlap.IntersectionArea(triangle, 0, 0, .5, 1), 9);
    }

    [Fact]
    public void MapsOccupiedAvailableAndUndecidedSlots()
    {
        var mapper = new SlotMapper(Slots());
        var result = mapper.Map(
        [
            new(.12f, .12f, .26f, .26f, .9f, "occupied"),
            new(.62f, .12f, .26f, .26f, .8f, "empty")
        ]);
        Assert.Equal("occupied", result[1].Status);
        Assert.Equal("available", result[2].Status);
        Assert.Null(result[3].Status);
        Assert.Equal("no_matching_detection", result[3].Reason);
    }

    [Fact]
    public void ConflictingLabelsWithinTheMarginAreUndecided()
    {
        var mapper = new SlotMapper(Slots());
        var result = mapper.Map(
        [
            new(.12f, .12f, .26f, .26f, .50f, "occupied"),
            new(.12f, .12f, .26f, .26f, .45f, "empty")
        ]);
        Assert.Null(result[1].Status);
        Assert.Equal("conflicting_labels", result[1].Reason);
    }

    [Fact]
    public void LowConfidenceAndCentreOutsideAreIgnored()
    {
        var mapper = new SlotMapper(Slots());
        var result = mapper.Map(
        [
            new(.12f, .12f, .26f, .26f, .05f, "occupied"),   // below 0.10
            new(.45f, .45f, .10f, .10f, .90f, "occupied")    // centre outside every polygon
        ]);
        Assert.All(result.Values, o => Assert.Null(o.Status));
    }

    [Fact]
    public void AgreesWithTheOpenCvMapper()
    {
        var detections = new List<BoundingBoxDto>
        {
            new(.12f, .12f, .26f, .26f, .90f, "occupied"),
            new(.58f, .08f, .30f, .30f, .70f, "empty"),
            new(.08f, .58f, .30f, .34f, .60f, "occupied"),
            new(.20f, .20f, .30f, .30f, .40f, "empty")
        };
        using var opencv = new RoiMapper(Slots());
        var expected = opencv.Map(detections, Width, Height);
        var actual = new SlotMapper(Slots()).Map(detections);
        foreach (var id in expected.Keys)
        {
            Assert.Equal(expected[id].Status, actual[id].Status);
            Assert.Equal(expected[id].Confidence, actual[id].Confidence, 6);
        }
    }
}
