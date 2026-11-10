namespace Pakar.Domain.Entities;

/// <summary>A user's report that a spot's displayed status is wrong (FR 5), reviewed by an admin (FR 16).</summary>
public class Report
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Description { get; set; } = string.Empty;
    public ReportStatus Status { get; set; } = ReportStatus.PendingReview;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public string? EvidenceImageUrl { get; set; }

    // Review outcome; empty while the report is pending.
    public string? AdminNote { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedById { get; set; }
    public AppUser? ReviewedBy { get; set; }

    // Foreign keys
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public Guid SpotId { get; set; }
    public ParkingSpot Spot { get; set; } = null!;

    /// <summary>
    /// Records an admin decision. Returns false when the report was already reviewed
    /// or the decision is not a final state (Resolved or Dismissed).
    /// </summary>
    public bool TryReview(ReportStatus decision, Guid reviewerId, string? note, DateTime reviewedAtUtc)
    {
        if (Status != ReportStatus.PendingReview || !IsFinal(decision))
            return false;

        Status = decision;
        ReviewedById = reviewerId;
        ReviewedAt = reviewedAtUtc;
        AdminNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        return true;
    }

    public static bool IsFinal(ReportStatus status) =>
        status is ReportStatus.Resolved or ReportStatus.Dismissed;
}
