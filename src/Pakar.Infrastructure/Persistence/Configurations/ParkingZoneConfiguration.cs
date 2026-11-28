using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pakar.Domain.Entities;

namespace Pakar.Infrastructure.Persistence.Configurations;

public sealed class ParkingZoneConfiguration : IEntityTypeConfiguration<ParkingZone>
{
    public void Configure(EntityTypeBuilder<ParkingZone> zone)
    {
        zone.ToTable("Zones");
        zone.Property(z => z.Name).HasMaxLength(100);
        zone.Property(z => z.Description).HasMaxLength(500);
        zone.Property(z => z.MapAreaJson).HasColumnType("jsonb");

        // FR 17: stored as text ("Full") so the column stays readable in the database.
        zone.Property(z => z.StatusOverride).HasConversion<string>().HasMaxLength(20);
        zone.Property(z => z.StatusOverrideNote).HasMaxLength(200);
        zone.Property(z => z.StatusOverrideSetBy).HasMaxLength(100);

        // Unique across removed zones too, so an AI pipeline ID is never reused.
        zone.HasIndex(z => z.ExternalId).IsUnique();

        // Removed zones disappear from every query; use IgnoreQueryFilters() to read history.
        zone.HasQueryFilter(z => z.DeletedAt == null);
    }
}
