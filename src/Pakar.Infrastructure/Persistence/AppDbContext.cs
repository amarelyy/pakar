using Microsoft.EntityFrameworkCore;
using Pakar.Domain.Entities;

namespace Pakar.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ParkingZone> Zones => Set<ParkingZone>();
    public DbSet<ParkingSpot> Spots => Set<ParkingSpot>();
    public DbSet<Camera> Cameras => Set<Camera>();
    public DbSet<Detection> Detections => Set<Detection>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<SavedZone> SavedZones => Set<SavedZone>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Table rules (lengths, indexes, relationships, soft-delete filters) live in Configurations/.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
