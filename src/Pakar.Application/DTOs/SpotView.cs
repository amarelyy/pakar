namespace Pakar.Application.DTOs;

/// <summary>A slot reading as published by the parking hub. It may be explicitly unknown.</summary>
public sealed record SpotView(int SpotId, string SpotNumber, int AreaId, string Status, double Confidence,
    DateTimeOffset UpdatedAt);
