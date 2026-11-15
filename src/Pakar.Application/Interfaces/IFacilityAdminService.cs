using Pakar.Application.Common;
using Pakar.Application.DTOs;

namespace Pakar.Application.Interfaces;

/// <summary>
/// FR 13, 14: admin management of zones and video sources (cameras). Removing a zone or camera hides it
/// but keeps its detection history.
/// </summary>
public interface IFacilityAdminService
{
    Task<IReadOnlyList<ZoneAdminDto>> ListZonesAsync(CancellationToken ct = default);
    Task<OperationResult<ZoneAdminDto>> GetZoneAsync(Guid id, CancellationToken ct = default);
    Task<OperationResult<ZoneAdminDto>> CreateZoneAsync(ZoneRequest request, CancellationToken ct = default);
    Task<OperationResult<ZoneAdminDto>> UpdateZoneAsync(Guid id, ZoneRequest request, CancellationToken ct = default);
    Task<OperationResult<bool>> DeleteZoneAsync(Guid id, CancellationToken ct = default);

    /// <param name="zoneId">When given, only the cameras of that zone.</param>
    Task<IReadOnlyList<CameraAdminDto>> ListCamerasAsync(Guid? zoneId, CancellationToken ct = default);
    Task<OperationResult<CameraAdminDto>> GetCameraAsync(Guid id, CancellationToken ct = default);
    Task<OperationResult<CameraAdminDto>> CreateCameraAsync(CameraRequest request, CancellationToken ct = default);
    Task<OperationResult<CameraAdminDto>> UpdateCameraAsync(Guid id, CameraRequest request, CancellationToken ct = default);
    Task<OperationResult<bool>> DeleteCameraAsync(Guid id, CancellationToken ct = default);
}
