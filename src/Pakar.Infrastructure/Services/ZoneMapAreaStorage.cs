using System.Text.Json;
using Pakar.Application.DTOs;

namespace Pakar.Infrastructure.Services;

internal static class ZoneMapAreaStorage
{
    internal static string? Serialize(ZoneMapAreaDto? area) => area is { Points.Length: > 0 }
        ? JsonSerializer.Serialize(area with { Label = string.IsNullOrWhiteSpace(area.Label) ? null : area.Label.Trim() })
        : null;

    internal static ZoneMapAreaDto? Deserialize(string? json) => json is null
        ? null
        : JsonSerializer.Deserialize<ZoneMapAreaDto>(json);
}
