using Microsoft.Extensions.Options;
using Pakar.AiInference.Services;
using Pakar.Shared.Dtos;

namespace Pakar.AiInference.Tests;

public class InferenceTests
{
    [Fact]
    public void NmsSuppressesSameClassButRetainsConflictingClass()
    {
        BoundingBoxDto box = new(.1f, .1f, .2f, .2f, .9f, "empty");
        var kept = OnnxInferenceEngine.Suppress([box, box with { Confidence = .8f },
            box with { Confidence = .7f, Label = "occupied" }], .7f);
        Assert.Equal(2, kept.Count);
        Assert.Equal(.9f, kept[0].Confidence);
        Assert.Equal("occupied", kept[1].Label);
    }

    [Fact]
    public void MissingModelFailsExplicitly() => Assert.Throws<ModelNotLoadedException>(() =>
        new OnnxInferenceEngine(Options.Create(new ModelOptions { Path = "missing-model.onnx" })));
}
