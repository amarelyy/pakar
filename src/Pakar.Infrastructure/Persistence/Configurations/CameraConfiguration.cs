using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pakar.Domain.Entities;

namespace Pakar.Infrastructure.Persistence.Configurations;

public sealed class CameraConfiguration : IEntityTypeConfiguration<Camera>
{
    public void Configure(EntityTypeBuilder<Camera> camera)
    {
        camera.Property(c => c.Name).HasMaxLength(100);
        camera.Property(c => c.StreamUrl).HasMaxLength(500);
        camera.Property(c => c.ConnectionType).HasMaxLength(20);
        camera.Property(c => c.ApiKeyHash).HasMaxLength(100);

        // Unique across removed cameras too, so an AI pipeline ID is never reused.
        camera.HasIndex(c => c.ExternalId).IsUnique();

        // Zones are soft-deleted, so a hard delete must never cascade into cameras.
        camera.HasOne(c => c.Zone)
            .WithMany(z => z.Cameras)
            .HasForeignKey(c => c.ZoneId)
            .OnDelete(DeleteBehavior.Restrict);

        camera.HasQueryFilter(c => c.DeletedAt == null && c.Zone.DeletedAt == null);
    }
}
