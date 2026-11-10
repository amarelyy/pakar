using Microsoft.EntityFrameworkCore;
using Pakar.Application.Common;
using Pakar.Application.DTOs;
using Pakar.Application.Interfaces;
using Pakar.Domain.Entities;
using Pakar.Infrastructure.Persistence;

namespace Pakar.Infrastructure.Services;

public sealed class ReportService(AppDbContext db, TimeProvider clock) : IReportService
{
    public const int MaxDescriptionLength = 1000;
    public const int MaxNoteLength = 500;
    public const int MaxUrlLength = 500;

    private const string ReceivedMessage = "Report received. It is pending review by an admin.";

    public async Task<OperationResult<ReportSubmissionDto>> SubmitAsync(Guid userId, SubmitReportRequest request, CancellationToken ct = default)
    {
        var errors = ValidateSubmission(request);
        if (errors.Count > 0)
            return OperationResult<ReportSubmissionDto>.Invalid(errors);

        // The spot query filter hides spots of removed zones, so those cannot be reported.
        if (!await db.Spots.AnyAsync(s => s.Id == request.SpotId, ct))
            return OperationResult<ReportSubmissionDto>.NotFound("Parking spot not found.");

        var report = new Report
        {
            SpotId = request.SpotId,
            UserId = userId,
            Description = request.Description.Trim(),
            EvidenceImageUrl = string.IsNullOrWhiteSpace(request.EvidenceImageUrl) ? null : request.EvidenceImageUrl.Trim(),
            Status = ReportStatus.PendingReview,
            SubmittedAt = clock.GetUtcNow().UtcDateTime
        };

        db.Reports.Add(report);
        await db.SaveChangesAsync(ct);

        return OperationResult<ReportSubmissionDto>.Ok(
            new ReportSubmissionDto(report.Id, report.Status.ToString(), ReceivedMessage, report.SubmittedAt));
    }

    public async Task<IReadOnlyList<ReportDto>> ListForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var rows = await Project(db.Reports
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.SubmittedAt))
            .ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<OperationResult<ReportDto>> GetAsync(Guid reportId, Guid requesterId, bool requesterIsAdmin, CancellationToken ct = default)
    {
        var row = await Project(db.Reports.Where(r => r.Id == reportId)).FirstOrDefaultAsync(ct);

        // Other users' reports are reported as missing so their existence is not revealed.
        if (row is null || (!requesterIsAdmin && row.SubmittedById != requesterId))
            return OperationResult<ReportDto>.NotFound("Report not found.");

        return OperationResult<ReportDto>.Ok(ToDto(row));
    }

    public async Task<OperationResult<PagedResult<ReportDto>>> ListAsync(string? status, int? page, int? pageSize, CancellationToken ct = default)
    {
        var query = db.Reports.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!TryParseStatus(status, out var parsed))
                return OperationResult<PagedResult<ReportDto>>.Invalid(nameof(status),
                    $"Status must be one of: {string.Join(", ", Enum.GetNames<ReportStatus>())}.");
            query = query.Where(r => r.Status == parsed);
        }

        var paging = PageRequest.From(page, pageSize);
        var total = await query.CountAsync(ct);
        var rows = await Project(query
                .OrderByDescending(r => r.SubmittedAt)
                .ThenBy(r => r.Id)
                .Skip(paging.Skip)
                .Take(paging.PageSize))
            .ToListAsync(ct);

        return OperationResult<PagedResult<ReportDto>>.Ok(
            new PagedResult<ReportDto>(rows.Select(ToDto).ToList(), paging.Page, paging.PageSize, total));
    }

    public async Task<OperationResult<ReportDto>> ReviewAsync(Guid reportId, Guid reviewerId, ReviewReportRequest request, CancellationToken ct = default)
    {
        if (!TryParseStatus(request.Decision, out var decision) || !Report.IsFinal(decision))
            return OperationResult<ReportDto>.Invalid(nameof(request.Decision), "Decision must be Resolved or Dismissed.");
        if (request.Note is { Length: > MaxNoteLength })
            return OperationResult<ReportDto>.Invalid(nameof(request.Note), $"Note must be at most {MaxNoteLength} characters.");

        var report = await db.Reports.FirstOrDefaultAsync(r => r.Id == reportId, ct);
        if (report is null)
            return OperationResult<ReportDto>.NotFound("Report not found.");

        if (!report.TryReview(decision, reviewerId, request.Note, clock.GetUtcNow().UtcDateTime))
            return OperationResult<ReportDto>.Conflict($"This report was already reviewed ({report.Status}).");

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return OperationResult<ReportDto>.Conflict("Another admin reviewed this report at the same time. Reload and try again.");
        }

        return await GetAsync(reportId, reviewerId, requesterIsAdmin: true, ct);
    }

    private static Dictionary<string, string[]> ValidateSubmission(SubmitReportRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.SpotId == Guid.Empty)
            errors[nameof(request.SpotId)] = ["SpotId is required."];

        var description = request.Description?.Trim() ?? string.Empty;
        if (description.Length == 0)
            errors[nameof(request.Description)] = ["Description is required."];
        else if (description.Length > MaxDescriptionLength)
            errors[nameof(request.Description)] = [$"Description must be at most {MaxDescriptionLength} characters."];

        if (!string.IsNullOrWhiteSpace(request.EvidenceImageUrl))
        {
            var url = request.EvidenceImageUrl.Trim();
            var isWebUrl = Uri.TryCreate(url, UriKind.Absolute, out var uri)
                           && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
            if (!isWebUrl || url.Length > MaxUrlLength)
                errors[nameof(request.EvidenceImageUrl)] = [$"EvidenceImageUrl must be an http(s) URL of at most {MaxUrlLength} characters."];
        }

        return errors;
    }

    private static bool TryParseStatus(string? value, out ReportStatus status) =>
        Enum.TryParse(value?.Trim(), ignoreCase: true, out status) && Enum.IsDefined(status);

    // Single projection shared by every read so all endpoints return the same shape.
    // Filter, sort and page before calling it; constructor projections cannot be sorted in SQL.
    private static IQueryable<ReportRow> Project(IQueryable<Report> query) => query
        .AsNoTracking()
        .Select(r => new ReportRow(
            r.Id,
            r.SpotId,
            r.Spot.Name,
            r.Spot.ZoneId,
            r.Spot.Zone.Name,
            r.Description,
            r.EvidenceImageUrl,
            r.Status,
            r.SubmittedAt,
            r.UserId,
            r.User.Name,
            r.ReviewedAt,
            r.ReviewedBy != null ? r.ReviewedBy.Name : null,
            r.AdminNote));

    private static ReportDto ToDto(ReportRow r) => new(
        r.Id, r.SpotId, r.SpotName, r.ZoneId, r.ZoneName, r.Description, r.EvidenceImageUrl,
        r.Status.ToString(), r.SubmittedAt, r.SubmittedById, r.SubmittedByName,
        r.ReviewedAt, r.ReviewedByName, r.AdminNote);

    private sealed record ReportRow(
        Guid Id,
        Guid SpotId,
        string SpotName,
        Guid ZoneId,
        string ZoneName,
        string Description,
        string? EvidenceImageUrl,
        ReportStatus Status,
        DateTime SubmittedAt,
        Guid SubmittedById,
        string SubmittedByName,
        DateTime? ReviewedAt,
        string? ReviewedByName,
        string? AdminNote);
}
