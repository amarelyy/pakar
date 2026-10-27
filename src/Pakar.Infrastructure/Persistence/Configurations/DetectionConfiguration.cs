using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pakar.Domain.Entities;

namespace Pakar.Infrastructure.Persistence.Configurations;

/// <summary>
/// Detections are history for statistics and prediction, so they are never removed by deleting
/// a camera, zone or spot, and they have no query filter.
/// </summary>
public sealed class DetectionConfiguration : IEntityTypeConfiguration<Detection>
{
    public void Configure(EntityTypeBuilder<Detection> detection)
    {
        detection.Property(d => d.Status).HasMaxLength(20);
        detection.Property(d => d.SnapshotUrl).HasMaxLength(500);

        // History queries filter by time range.
        detection.HasIndex(d => d.Timestamp);

        detection.HasOne(d => d.Camera)
            .WithMany()
            .HasForeignKey(d => d.CameraId)
            .OnDelete(DeleteBehavior.Restrict);

        detection.HasOne(d => d.Spot)
            .WithMany()
            .HasForeignKey(d => d.SpotId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
