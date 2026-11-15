using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pakar.Domain.Entities;

namespace Pakar.Infrastructure.Persistence.Configurations;

public sealed class SpotRoiConfiguration : IEntityTypeConfiguration<SpotRoi>
{
    public void Configure(EntityTypeBuilder<SpotRoi> roi)
    {
        roi.ToTable("SpotRois");
        roi.Property(r => r.PointsJson).HasColumnType("jsonb").IsRequired();

        // One polygon per spot per video source.
        roi.HasIndex(r => new { r.CameraId, r.SpotId }).IsUnique();

        // Cameras, zones and spots are soft-deleted, so a hard delete must never cascade into polygons.
        roi.HasOne(r => r.Camera)
            .WithMany(c => c.Rois)
            .HasForeignKey(r => r.CameraId)
            .OnDelete(DeleteBehavior.Restrict);

        roi.HasOne(r => r.Spot)
            .WithMany(s => s.Rois)
            .HasForeignKey(r => r.SpotId)
            .OnDelete(DeleteBehavior.Restrict);

        // Polygons of a removed camera, zone or spot disappear with them.
        roi.HasQueryFilter(r => r.Camera.DeletedAt == null && r.Camera.Zone.DeletedAt == null && r.Spot.Zone.DeletedAt == null);
    }
}
