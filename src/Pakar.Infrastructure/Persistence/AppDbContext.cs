using Microsoft.EntityFrameworkCore;
using Pakar.Domain.Entities;

namespace Pakar.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    // Constructor utama untuk Dependency Injection saat aplikasi berjalan
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Daftar Tabel (DbSet) sesuai Entity yang kita buat
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

        // Konfigurasi Relasi: Zone memiliki banyak Spot
        modelBuilder.Entity<ParkingSpot>()
            .HasOne(s => s.Zone)
            .WithMany(z => z.Spots)
            .HasForeignKey(s => s.ZoneId);

        // Konfigurasi Relasi: Zone memiliki banyak Camera
        modelBuilder.Entity<Camera>()
            .HasOne(c => c.Zone)
            .WithMany(z => z.Cameras)
            .HasForeignKey(c => c.ZoneId);
            
        // Konfigurasi Relasi: User membuat banyak Report
        modelBuilder.Entity<Report>()
            .HasOne(r => r.User)
            .WithMany(u => u.Reports)
            .HasForeignKey(r => r.UserId);

        // Konfigurasi Relasi: Report terkait dengan satu Spot
        modelBuilder.Entity<Report>()
            .HasOne(r => r.Spot)
            .WithMany(s => s.Reports)
            .HasForeignKey(r => r.SpotId);
    }
}