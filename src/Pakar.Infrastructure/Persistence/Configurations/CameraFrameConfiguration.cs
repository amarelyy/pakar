using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pakar.Domain.Entities;

namespace Pakar.Infrastructure.Persistence.Configurations;

public sealed class CameraFrameConfiguration : IEntityTypeConfiguration<CameraFrame>
{
    public void Configure(EntityTypeBuilder<CameraFrame> frame)
    {
        frame.ToTable("CameraFrames");
        frame.HasKey(f => f.CameraId);
        frame.Property(f => f.ContentType).HasMaxLength(20).IsRequired();
        frame.Property(f => f.Data).HasColumnType("bytea").IsRequired();
        frame.HasOne(f => f.Camera).WithOne(c => c.ReferenceFrame).HasForeignKey<CameraFrame>(f => f.CameraId).OnDelete(DeleteBehavior.Restrict);
        frame.HasQueryFilter(f => f.Camera.DeletedAt == null && f.Camera.Zone.DeletedAt == null);
    }
}
