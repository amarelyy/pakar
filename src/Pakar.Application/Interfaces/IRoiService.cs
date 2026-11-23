using Pakar.Application.Common;
using Pakar.Shared.Dtos;

namespace Pakar.Application.Interfaces;

public interface IRoiService
{
    Task<OperationResult<CameraRoisDto>> GetAsync(Guid cameraId, CancellationToken ct);
    Task<OperationResult<CameraRoisDto>> ReplaceAsync(Guid cameraId, RoiRequest request, CancellationToken ct);
    Task<IReadOnlyList<WorkerSourceDto>> GetSourcesAsync(CancellationToken ct);
}
