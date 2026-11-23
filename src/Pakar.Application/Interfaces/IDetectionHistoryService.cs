using Pakar.Application.Common;
using Pakar.Application.DTOs;

namespace Pakar.Application.Interfaces;

/// <summary>Read-only access to stored detections, for charts and prediction.</summary>
public interface IDetectionHistoryService
{
    /// <summary>Occupancy of one zone per hour or day. Public: it contains no per-slot or per-user data.</summary>
    Task<OperationResult<ZoneHistoryDto>> GetZoneSeriesAsync(Guid zoneId, ZoneHistoryQuery query, CancellationToken ct = default);

    /// <summary>Raw detection records, newest first, for admins.</summary>
    Task<OperationResult<PagedResult<DetectionRecordDto>>> ListRecordsAsync(DetectionHistoryQuery query, CancellationToken ct = default);
}
