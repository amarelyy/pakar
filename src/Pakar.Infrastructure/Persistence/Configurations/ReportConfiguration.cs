using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pakar.Domain.Entities;

namespace Pakar.Infrastructure.Persistence.Configurations;

public sealed class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> report)
    {
        report.Property(r => r.Description).HasMaxLength(1000);
        report.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        report.Property(r => r.EvidenceImageUrl).HasMaxLength(500);
        report.Property(r => r.AdminNote).HasMaxLength(500);

        // Postgres xmin as a concurrency token: two admins reviewing the same report cannot both win.
        report.Property<uint>("Version").IsRowVersion();

        // Admin lists filter by status and sort by newest.
        report.HasIndex(r => new { r.Status, r.SubmittedAt });

        report.HasOne(r => r.User)
            .WithMany(u => u.Reports)
            .HasForeignKey(r => r.UserId);

        report.HasOne(r => r.ReviewedBy)
            .WithMany()
            .HasForeignKey(r => r.ReviewedById)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        report.HasOne(r => r.Spot)
            .WithMany(s => s.Reports)
            .HasForeignKey(r => r.SpotId);

        // Follows the spot filter so reports of removed zones are hidden consistently.
        report.HasQueryFilter(r => r.Spot.Zone.DeletedAt == null);
    }
}
