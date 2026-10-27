using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pakar.Domain.Entities;

namespace Pakar.Infrastructure.Persistence.Configurations;

public sealed class SavedZoneConfiguration : IEntityTypeConfiguration<SavedZone>
{
    public void Configure(EntityTypeBuilder<SavedZone> savedZone)
    {
        // A removed zone disappears from users' saved lists.
        savedZone.HasQueryFilter(s => s.Zone.DeletedAt == null);
    }
}
