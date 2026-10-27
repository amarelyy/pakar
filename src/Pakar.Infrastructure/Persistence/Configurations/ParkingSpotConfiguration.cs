using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pakar.Domain.Entities;

namespace Pakar.Infrastructure.Persistence.Configurations;

public sealed class ParkingSpotConfiguration : IEntityTypeConfiguration<ParkingSpot>
{
    public void Configure(EntityTypeBuilder<ParkingSpot> spot)
    {
        spot.ToTable("Spots");
        spot.Property(s => s.Name).HasMaxLength(50);
        spot.HasIndex(s => s.ExternalId);

        spot.HasOne(s => s.Zone)
            .WithMany(z => z.Spots)
            .HasForeignKey(s => s.ZoneId)
            .OnDelete(DeleteBehavior.Restrict);

        // Spots of a removed zone are hidden together with the zone.
        spot.HasQueryFilter(s => s.Zone.DeletedAt == null);
    }
}
