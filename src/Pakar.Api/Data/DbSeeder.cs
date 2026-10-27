using Microsoft.EntityFrameworkCore;
using Pakar.Api.Security;
using Pakar.Domain.Entities;
using Pakar.Infrastructure.Persistence;

namespace Pakar.Api.Data;

public static class DbSeeder
{
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
            // Building H (Civil Engineering)
            var zoneH = new ParkingZone 
            { 
                Id = Guid.NewGuid(), 
                Name = "Building H Parking (Civil Engineering)", 
                Description = "Front area of the Department of Civil and Environmental Engineering",
                TotalCapacity = 10,
                ExternalId = 1
            };
            
            // Building I (SGLC)
            var zoneI = new ParkingZone 
            { 
                Id = Guid.NewGuid(), 
                Name = "Building I Parking (Faculty Office)", 
                Description = "Main lobby area of the Faculty central office",
                TotalCapacity = 10,
                ExternalId = 2
            };

            // West of Building E (Geological Engineering)
            var zoneE = new ParkingZone 
            { 
                Id = Guid.NewGuid(), 
                Name = "Building E West Parking (Geological Engineering)", 
                Description = "Area 100 m west of the Department of Geological Engineering",
                TotalCapacity = 10,
                ExternalId = 3
            };

            await context.Zones.AddRangeAsync(zoneH, zoneI, zoneE);
            await context.SaveChangesAsync();

            var spots = new List<ParkingSpot>();

            // Building H: 10 spots
            for (int i = 1; i <= 10; i++)
            {
                spots.Add(new ParkingSpot
                {
                    ZoneId = zoneH.Id,
                    Name = $"H-{i:D2}",
                    Latitude = -7.7685 + (i * 0.00005),
                    Longitude = 110.3765,
                    IsOccupied = false,
                    ExternalId = i
                });
            }

            // Building I: 10 spots
            for (int i = 1; i <= 10; i++)
            {
                spots.Add(new ParkingSpot
                {
                    ZoneId = zoneI.Id,
                    Name = $"I-{i:D2}",
                    Latitude = -7.7690,
                    Longitude = 110.3770 + (i * 0.00005),
                    IsOccupied = false,
                    ExternalId = 100 + i
                });
            }

            // Building E West: 10 spots
            for (int i = 1; i <= 10; i++)
            {
                spots.Add(new ParkingSpot
                {
                    ZoneId = zoneE.Id,
                    Name = $"E-W-{i:D2}",
                    Latitude = -7.7680,
                    Longitude = 110.3750 - (i * 0.00005),
                    IsOccupied = false,
                    ExternalId = 200 + i
                });
            }

            await context.Spots.AddRangeAsync(spots);

            // One camera per zone; ExternalId matches the AI pipeline configuration.
            var cameras = new List<Camera>
            {
                new Camera
                {
                    Name = "Building H Camera",
                    StreamUrl = "rtsp://admin:admin@192.168.1.101:554/stream1",
                    ConnectionType = "IP",
                    ApiKeyHash = "temp-hash",
                    ZoneId = zoneH.Id,
                    ExternalId = 1
                },
                new Camera
                {
                    Name = "Building I Camera",
                    StreamUrl = "rtsp://admin:admin@192.168.1.102:554/stream1",
                    ConnectionType = "IP",
                    ApiKeyHash = "temp-hash",
                    ZoneId = zoneI.Id,
                    ExternalId = 2
                },
                new Camera
                {
                    Name = "Building E West Camera",
                    StreamUrl = "rtsp://admin:admin@192.168.1.103:554/stream1",
                    ConnectionType = "IP",
                    ApiKeyHash = "temp-hash",
                    ZoneId = zoneE.Id,
                    ExternalId = 3
                }
            };
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