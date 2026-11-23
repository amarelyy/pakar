using Pakar.Application.Common;
using Pakar.Application.DTOs;

namespace Pakar.Application.Interfaces;

/// <summary>Manual zone status for admins, used while a camera is down (FR 17).</summary>
public interface IZoneOverrideService
{
    Task<IReadOnlyList<ZoneOverrideDto>> ListAsync(CancellationToken ct = default);

    Task<OperationResult<ZoneOverrideDto>> SetAsync(Guid zoneId, Guid adminId, SetZoneOverrideRequest request, CancellationToken ct = default);

    /// <summary>Removes the override so the zone shows its computed status again. Clearing a zone without one succeeds.</summary>
    Task<OperationResult<ZoneRefDto>> ClearAsync(Guid zoneId, CancellationToken ct = default);
}
