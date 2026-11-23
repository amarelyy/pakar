namespace Pakar.Vision;

/// <summary>What the detector decided about one slot in one frame; Status is null when undecided.</summary>
public sealed record Observation(string? Status, double Confidence, string? Reason);
