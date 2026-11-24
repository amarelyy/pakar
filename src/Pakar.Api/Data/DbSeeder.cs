using Microsoft.EntityFrameworkCore;
using Pakar.Api.Security;
using Pakar.Domain.Entities;
using Pakar.Infrastructure.Persistence;

namespace Pakar.Api.Data;

public static class DbSeeder
{
    /// <summary>
    /// The four parking areas of the Faculty of Engineering, UGM. <c>Latitude</c> and <c>Longitude</c> are the
    /// department building pins from Google Maps; the spots are laid out in one east-west row just south of the
    /// pin. These positions are approximate until the real slot coordinates are surveyed.
    /// </summary>
    private sealed record SeedZone(
        int ExternalId, string Name, string Description, string SpotPrefix, string CameraName,
        double Latitude, double Longitude);

    private static readonly SeedZone[] SeedZones =
    [
        new(1, "Teknik Kimia Parking", "Parking area of the Department of Chemical Engineering",
            "TK", "Teknik Kimia Camera", -7.765307, 110.371418),
        new(2, "Teknik Mesin dan Industri Parking", "Parking area of the Department of Mechanical and Industrial Engineering",
            "TMI", "Teknik Mesin dan Industri Camera", -7.7658044, 110.3711988),
        new(3, "Teknik Elektro dan Teknologi Informasi Parking", "Parking area of the Department of Electrical and Information Engineering",
            "TETI", "Teknik Elektro dan Teknologi Informasi Camera", -7.7657811, 110.3717409),
        new(4, "Teknik Nuklir dan Teknik Fisika Parking", "Parking area of the Department of Nuclear and Physics Engineering",
            "TNTF", "Teknik Nuklir dan Teknik Fisika Camera", -7.7662333, 110.3728),
    ];

    private const int SpotsPerZone = 10;
    private const double RowOffsetLatitude = -0.00008; // about 9 m south of the building pin
    private const double SpotSpacingLongitude = 0.00004; // about 4.4 m between neighbouring spots

    public static async Task SeedAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Development migrates automatically. Production applies schema changes as a deployment step
        // (migration bundle) unless Database:ApplyMigrationsOnStartup is set to true.
        var applyMigrations = app.Configuration.GetValue<bool?>("Database:ApplyMigrationsOnStartup")
                              ?? app.Environment.IsDevelopment();
        if (applyMigrations)
            await context.Database.MigrateAsync();

        if (!await context.Zones.AnyAsync())
        {
            var zones = new List<ParkingZone>();
            var spots = new List<ParkingSpot>();
            var cameras = new List<Camera>();

            foreach (var seed in SeedZones)
            {
                var zone = new ParkingZone
                {
                    Id = Guid.NewGuid(),
                    Name = seed.Name,
                    Description = seed.Description,
                    TotalCapacity = SpotsPerZone,
                    ExternalId = seed.ExternalId
                };
                zones.Add(zone);

                // Spot ids are unique across zones: zone 1 uses 1-10, zone 2 uses 101-110, and so on.
                for (var i = 1; i <= SpotsPerZone; i++)
                {
                    spots.Add(new ParkingSpot
                    {
                        ZoneId = zone.Id,
                        Name = $"{seed.SpotPrefix}-{i:D2}",
                        Latitude = seed.Latitude + RowOffsetLatitude,
                        Longitude = seed.Longitude + (i - 1) * SpotSpacingLongitude,
                        IsOccupied = false,
                        ExternalId = (seed.ExternalId - 1) * 100 + i
                    });
                }

                // One video source per zone; ExternalId matches the AI worker configuration. The video file name is
                // empty until a video is available; an admin sets it (PUT /api/cameras/{id}). No real camera is used.
                cameras.Add(new Camera
                {
                    Name = seed.CameraName,
                    StreamUrl = string.Empty,
                    ConnectionType = "Video",
                    ApiKeyHash = "temp-hash",
                    ZoneId = zone.Id,
                    ExternalId = seed.ExternalId
                });
            }

            await context.Zones.AddRangeAsync(zones);
            await context.SaveChangesAsync();

            await context.Spots.AddRangeAsync(spots);
            await context.Cameras.AddRangeAsync(cameras);
            await context.SaveChangesAsync();
        }

        await SeedAdminAsync(context, app.Configuration, app.Logger);
    }

    /// <summary>
    /// Creates the initial administrator from the "Admin" configuration section (Admin:Email,
    /// Admin:Password, optional Admin:Name). Skipped when unset or when the account already exists,
    /// so credentials can be removed from configuration after first start.
    /// </summary>
    private static async Task SeedAdminAsync(AppDbContext context, IConfiguration config, ILogger logger)
    {
        var email = config["Admin:Email"];
        var password = config["Admin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        email = EmailAddresses.Normalize(email);
        if (await context.Users.AnyAsync(u => u.Email == email))
            return;

        if (password.Length < 12)
            throw new InvalidOperationException("Admin:Password must be at least 12 characters.");

        context.Users.Add(new AppUser
        {
            Name = config["Admin:Name"] ?? "Administrator",
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = Roles.Admin
        });
        await context.SaveChangesAsync();
        logger.LogInformation("Seeded administrator account {Email}.", email);
    }
}
