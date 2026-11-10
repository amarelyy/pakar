namespace Pakar.Application.DTOs;

/// <summary>FR 5: body of a new inaccuracy report.</summary>
public sealed record SubmitReportRequest(Guid SpotId, string Description, string? EvidenceImageUrl = null);

/// <summary>FR 6: returned right after submission so the user sees the status immediately.</summary>
public sealed record ReportSubmissionDto(Guid Id, string Status, string Message, DateTime SubmittedAt);

/// <summary>FR 16: admin decision. Decision is "Resolved" or "Dismissed".</summary>
public sealed record ReviewReportRequest(string Decision, string? Note = null);

public sealed record ReportDto(
    Guid Id,
    Guid SpotId,
    string SpotName,
    Guid ZoneId,
    string ZoneName,
    string Description,
    string? EvidenceImageUrl,
    string Status,
    DateTime SubmittedAt,
    Guid SubmittedById,
    string SubmittedByName,
    DateTime? ReviewedAt,
    string? ReviewedByName,
    string? AdminNote);
