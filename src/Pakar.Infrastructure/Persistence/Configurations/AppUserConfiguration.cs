using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pakar.Domain.Entities;

namespace Pakar.Infrastructure.Persistence.Configurations;

public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> user)
    {
        user.ToTable("Users");
        user.Property(u => u.Name).HasMaxLength(100);
        user.Property(u => u.Email).HasMaxLength(256);
        user.Property(u => u.PasswordHash).HasMaxLength(100);
        user.Property(u => u.Role).HasMaxLength(20);

        // FR 7: one account per email, enforced by the database even under concurrent registrations.
        user.HasIndex(u => u.Email).IsUnique();
    }
}
