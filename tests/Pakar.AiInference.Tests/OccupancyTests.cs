using Pakar.AiInference.Detection;
using Pakar.Vision;
using Pakar.Shared.Dtos;
using Pakar.Shared.Geometry;

namespace Pakar.AiInference.Tests;

public class OccupancyTests
{
    private static double[][] Square(double start = .1, double end = .4) => [[start, start], [end, start], [end, end], [start, end]];
    private static List<WorkerSlotDto> Slots() => [new(1, "A", Square()), new(2, "B", Square(.6, .9))];
    private static Dictionary<int, Observation> Observed(string? a, string? b = "available") =>
        new() { [1] = new(a, .8, a is null ? "missing" : null), [2] = new(b, .9, b is null ? "missing" : null) };

    [Theory]
    [InlineData(.1, .1, true)]
    [InlineData(.2, .2, true)]
    [InlineData(.4, .25, true)]
    [InlineData(.5, .5, false)]
    public void PolygonIncludesBoundary(double x, double y, bool expected) => Assert.Equal(expected, PolygonRules.Contains(Square(), x, y));

    [Fact]
    public void LabelsMissingAndConflictsAreExplicit()
    {
        using var mapper = new RoiMapper(Slots());
        BoundingBoxDto box = new(.1f, .1f, .3f, .3f, .8f, "empty");
        Assert.Equal("available", mapper.Map([box], 1000, 1000)[1].Status);
        Assert.Null(mapper.Map([], 1000, 1000)[1].Status);
        Assert.Equal("conflicting_labels", mapper.Map([box, box with { Label = "occupied", Confidence = .75f }], 1000, 1000)[1].Reason);
        Assert.Equal("occupied", mapper.Map([box, box with { Label = "occupied", Confidence = .95f }], 1000, 1000)[1].Status);
        Assert.Null(mapper.Map([box with { Confidence = .09f }], 1000, 1000)[1].Status);
        Assert.Null(mapper.Map([box with { Label = "car" }], 1000, 1000)[1].Status);
    }

    [Fact]
    public void OneBoxCannotFillTwoOverlappingSlots()
    {
        using var mapper = new RoiMapper([new(1, "A", Square()), new(2, "B", Square())]);
        Assert.Single(mapper.Map([new(.1f, .1f, .3f, .3f, .8f, "occupied")], 1000, 1000).Values, v => v.Status != null);
    }

    [Fact]
    public void CenterOutsideOrInsufficientIouIsUndecided()
    {
        using var mapper = new RoiMapper(Slots());
        Assert.Null(mapper.Map([new(.35f, .1f, .3f, .3f, .8f, "empty")], 1000, 1000)[1].Status);
        Assert.Null(mapper.Map([new(.2f, .2f, .01f, .01f, .8f, "empty")], 1000, 1000)[1].Status);
    }

    [Fact]
    public void DuplicateIdsOrNamesRejected()
    {
        Assert.Throws<ArgumentException>(() => new RoiMapper([new(1, "A", Square()), new(1, "B", Square())]));
        Assert.Throws<ArgumentException>(() => new RoiMapper([new(1, "A", Square()), new(2, "A", Square())]));
    }

    [Fact]
    public void DebounceUsesTwoAndThreeSeconds()
    {
        var tracker = new OccupancyTracker(Slots());
        tracker.Update(Observed("available"), 0);
        Assert.Empty(tracker.Update(Observed("occupied"), 1).Changes);
        Assert.Empty(tracker.Update(Observed("occupied"), 2.9).Changes);
        Assert.Single(tracker.Update(Observed("occupied"), 3).Changes);
        Assert.Empty(tracker.Update(Observed("available"), 4).Changes);
        Assert.Single(tracker.Update(Observed("available"), 7).Changes);
    }

    [Fact]
    public void UnknownAndInvalidGapResetCandidateAndOmitSlot()
    {
        var tracker = new OccupancyTracker(Slots());
        tracker.Update(Observed("available"), 0);
        tracker.Update(Observed("occupied"), 1);
        var missing = tracker.Update(Observed(null), 2);
        Assert.DoesNotContain(missing.Visible, s => s.SpotId == 1);
        Assert.Empty(tracker.Update(Observed("occupied"), 3).Changes);
        tracker.ResetPending();
        Assert.Empty(tracker.Update(Observed("occupied"), 10).Changes);
        Assert.Single(tracker.Update(Observed("occupied"), 12).Changes);
    }

    [Fact]
    public void BackwardMediaClockCannotCompleteCandidate()
    {
        var tracker = new OccupancyTracker(Slots());
        tracker.Update(Observed("available"), 100);
        tracker.Update(Observed("occupied"), 101);
        Assert.Empty(tracker.Update(Observed("occupied"), 0).Changes);
        Assert.Single(tracker.Update(Observed("occupied"), 2).Changes);
    }

    [Fact]
    public void SwapUpdatesBothSlotsEvenWhenZoneTotalDoesNotChange()
    {
        var tracker = new OccupancyTracker(Slots());
        var initial = tracker.Update(Observed("occupied", "available"), 0);
        tracker.Update(Observed("available", "occupied"), 1);
        var swapped = tracker.Update(Observed("available", "occupied"), 4);
        Assert.Equal(initial.Occupied, swapped.Occupied);
        Assert.Equal(2, swapped.Changes.Count);
    }

    [Fact]
    public void CountsIncludeUnconfiguredAndPendingUncertainty()
    {
        var tracker = new OccupancyTracker(Slots());
        tracker.Update(Observed("occupied"), 0, 5);
        var pending = tracker.Update(Observed("available", null), 1, 5);
        Assert.Equal(0, pending.OccupiedMin); Assert.Equal(5, pending.OccupiedMax);
        Assert.Equal(1, pending.Pending);
        Assert.Single(pending.Unknown);
        Assert.Empty(pending.Confirmed);
    }

    [Fact]
    public void ReleasingAnOccupiedSlotRequiresStrongerEvidenceThanTheFirstReading()
    {
        var tracker = new OccupancyTracker(Slots(), availableConfirmationConfidence: .35);
        var weak = Observed("available", null);
        weak[1] = new Observation("available", .22, null);

        Assert.Equal("available", Assert.Single(tracker.Update(weak, 0).Confirmed).Status);
        Assert.Empty(tracker.Update(Observed("occupied", null), 1).Confirmed);
        Assert.Equal("occupied", Assert.Single(tracker.Update(Observed("occupied", null), 3).Confirmed).Status);
        Assert.Empty(tracker.Update(weak, 4).Confirmed);
        Assert.Empty(tracker.Update(weak, 7).Confirmed);

        Assert.Equal("available", Assert.Single(tracker.Update(Observed("available", null), 8).Confirmed).Status);
        Assert.Single(tracker.Update(weak, 9).Confirmed);
        Assert.Empty(tracker.Update(Observed(null, null), 10).Confirmed);
    }

    [Fact]
    public void PolygonRejectsInvalidCoordinatesTinyAreaAndCrossing()
    {
        Assert.NotNull(PolygonRules.Validate([[0, 0], [1, 1], [0, 1], [1, 0]]));
        Assert.NotNull(PolygonRules.Validate(Square(.1, .101)));
        Assert.NotNull(PolygonRules.Validate([[0, 0], [double.NaN, 1], [1, 0]]));
        Assert.NotNull(PolygonRules.Validate([[0, 0], [2, 1], [1, 0]]));
        Assert.Null(PolygonRules.Validate(Square()));
    }
}
