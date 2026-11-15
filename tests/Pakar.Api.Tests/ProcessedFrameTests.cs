using Pakar.Application.DTOs;
using Pakar.Application.Validation;
using Pakar.Shared.Dtos;

namespace Pakar.Api.Tests;

public sealed class ProcessedFrameTests
{
    private static readonly DateTimeOffset Now=DateTimeOffset.Parse("2026-11-15T12:00:00Z");
    private static ProcessedFrameDto Frame(DateTimeOffset? at=null)=>new(new(1,1,at??Now,0,false,[new(1,"A","available",null,.9)]),"clip.mp4",Convert.FromBase64String("/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAIBAQEBAQIBAQECAgICAgQDAgICAgUEBAMEBgUGBgYFBgYGBwkIBgcJBwYGCAsICQoKCgoKBggLDAsKDAkKCgr/2wBDAQICAgICAgUDAwUKBwYHCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgr/wAARCAACAAIDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD+f+iiigD/2Q=="),[new(1,"A",[[.1,.1],[.4,.1],[.4,.4],[.1,.4]])],[]);
    [Fact] public void AValidFramePassesAllStructuralRules()=>Assert.Null(ProcessedFrameRules.Validate(Frame(),Now));
    [Fact] public void NullEntriesAndNullPointsAreRejectedWithoutThrowing()
    {
        var a=Frame();a.Observation.Slots[0]=null!;Assert.NotNull(ProcessedFrameRules.Validate(a,Now));
        var b=Frame();b.Polygons[0]=null!;Assert.NotNull(ProcessedFrameRules.Validate(b,Now));
        var c=Frame();c.Polygons[0].Polygon[0]=null!;Assert.NotNull(ProcessedFrameRules.Validate(c,Now));
        var d=Frame();d.Boxes.Add(null!);Assert.NotNull(ProcessedFrameRules.Validate(d,Now));
    }
    [Fact] public void NonFiniteCoordinatesAndInvalidConfidenceAreRejected()
    {
        var a=Frame();a.Polygons[0].Polygon[0][0]=double.NaN;Assert.NotNull(ProcessedFrameRules.Validate(a,Now));
        var b=Frame();b.Observation.Slots[0]=b.Observation.Slots[0] with {Confidence=1.1};Assert.NotNull(ProcessedFrameRules.Validate(b,Now));
        Assert.NotNull(ProcessedFrameRules.Validate(Frame() with {VideoFile="../clip.mp4"},Now));
        Assert.NotNull(ProcessedFrameRules.Validate(Frame(Now.AddMinutes(-1)),Now));
    }
    [Fact] public void PixelsAndMetadataCannotBeRewoundByLatePublish()
    {
        var observations=new CurrentObservations();var frames=new ProcessedFrameStore(observations);var newest=Frame();
        Assert.True(frames.TryPublish(newest));Assert.False(frames.TryPublish(Frame(Now.AddSeconds(-1))));
        Assert.Same(newest,frames.Get(1));Assert.Equal(newest.Observation,observations.Snapshot().Single());
    }
    [Fact] public async Task ConcurrentPublishKeepsNewestPixelsAndMatchingMetadata()
    {
        var observations=new CurrentObservations();var frames=new ProcessedFrameStore(observations);
        await Task.WhenAll(Enumerable.Range(0,64).Select(i=>Task.Run(()=>frames.TryPublish(Frame(Now.AddTicks(i))))));
        var latest=frames.Get(1)!;
        Assert.Equal(Now.AddTicks(63),latest.Observation.ObservedAt);
        Assert.Same(latest.Observation,observations.Snapshot().Single());
    }
    [Fact] public void ConfigurationChangeClearsPixelsAndImmediatelyInvalidatesEmptyEvidence()
    {
        var observations=new CurrentObservations();var frames=new ProcessedFrameStore(observations);frames.TryPublish(Frame());
        frames.Invalidate(1,Now.AddMilliseconds(1));Assert.Null(frames.Get(1));Assert.Null(observations.Status(1,Now.AddMilliseconds(1)));
        Assert.False(frames.TryPublish(Frame()));Assert.True(frames.TryPublish(Frame(Now.AddSeconds(1))));
        Assert.Equal("available",observations.Status(1,Now.AddSeconds(1)));
    }
}
