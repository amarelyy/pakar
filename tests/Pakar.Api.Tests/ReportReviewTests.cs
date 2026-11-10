using Pakar.Domain.Entities;

namespace Pakar.Api.Tests;

public class ReportReviewTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 8, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Reviewer = Guid.NewGuid();

    [Fact]
    public void New_report_is_pending_review()
    {
        Assert.Equal(ReportStatus.PendingReview, new Report().Status);
    }

    [Theory]
    [InlineData(ReportStatus.Resolved)]
    [InlineData(ReportStatus.Dismissed)]
    public void Pending_report_can_be_reviewed_into_final_state(ReportStatus decision)
    {
        var report = new Report();

        var reviewed = report.TryReview(decision, Reviewer, "  checked on site  ", Now);

        Assert.True(reviewed);
        Assert.Equal(decision, report.Status);
        Assert.Equal(Reviewer, report.ReviewedById);
        Assert.Equal(Now, report.ReviewedAt);
        Assert.Equal("checked on site", report.AdminNote);
    }

    [Fact]
    public void Review_cannot_return_report_to_pending()
    {
        var report = new Report();

        Assert.False(report.TryReview(ReportStatus.PendingReview, Reviewer, null, Now));
        Assert.Null(report.ReviewedAt);
    }

    [Fact]
    public void Reviewed_report_cannot_be_reviewed_again()
    {
        var report = new Report();
        report.TryReview(ReportStatus.Resolved, Reviewer, null, Now);

        Assert.False(report.TryReview(ReportStatus.Dismissed, Guid.NewGuid(), "late", Now.AddHours(1)));
        Assert.Equal(ReportStatus.Resolved, report.Status);
        Assert.Equal(Reviewer, report.ReviewedById);
    }

    [Fact]
    public void Blank_note_is_stored_as_null()
    {
        var report = new Report();
        report.TryReview(ReportStatus.Dismissed, Reviewer, "   ", Now);

        Assert.Null(report.AdminNote);
    }
}
