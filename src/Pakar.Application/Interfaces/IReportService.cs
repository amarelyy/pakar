using Pakar.Application.Common;
using Pakar.Application.DTOs;

namespace Pakar.Application.Interfaces;

/// <summary>Inaccuracy reports: submission by users (FR 5, FR 6) and review by admins (FR 16).</summary>
public interface IReportService
{
    Task<OperationResult<ReportSubmissionDto>> SubmitAsync(Guid userId, SubmitReportRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<ReportDto>> ListForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Returns the report when the requester owns it or is an admin; otherwise NotFound.</summary>
    Task<OperationResult<ReportDto>> GetAsync(Guid reportId, Guid requesterId, bool requesterIsAdmin, CancellationToken ct = default);

    Task<OperationResult<PagedResult<ReportDto>>> ListAsync(string? status, int? page, int? pageSize, CancellationToken ct = default);

    Task<OperationResult<ReportDto>> ReviewAsync(Guid reportId, Guid reviewerId, ReviewReportRequest request, CancellationToken ct = default);
}
