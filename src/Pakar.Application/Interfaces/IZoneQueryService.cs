using Pakar.Application.Common;
using Pakar.Application.DTOs;

namespace Pakar.Application.Interfaces;

/// <summary>Read-only zone availability for guests and users (FR 1 to FR 4).</summary>
public interface IZoneQueryService
{
    Task<IReadOnlyList<ZoneDto>> ListAsync(CancellationToken ct = default);

    Task<OperationResult<IReadOnlyList<ZoneSummaryDto>>> SearchAsync(ZoneSearchQuery query, CancellationToken ct = default);

    /// <summary>
    /// Nearest zone with free spots, measured from the given point or, when none is given,
    /// from the center of the requested zone's spots.
    /// </summary>
    Task<OperationResult<AlternateZoneDto>> FindAlternateAsync(Guid zoneId, double? lat, double? lng, CancellationToken ct = default);
}
