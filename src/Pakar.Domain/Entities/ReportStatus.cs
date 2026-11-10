namespace Pakar.Domain.Entities;

/// <summary>Review state of an inaccuracy report (FR 6, FR 16). Stored as text.</summary>
public enum ReportStatus
{
    /// <summary>Received and waiting for an admin to review it.</summary>
    PendingReview,

    /// <summary>An admin confirmed the problem and handled it.</summary>
    Resolved,

    /// <summary>An admin reviewed the report and found no problem.</summary>
    Dismissed
}
