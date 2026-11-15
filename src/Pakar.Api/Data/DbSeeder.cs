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
            // Team scope confirmed 2026-11-15: four representative parking areas, dummy video only.
            // Coordinates and capacity below are DEMO fixtures, not surveyed parking boundaries/capacities.
            var definitions = new[]
            {
                (Code: "DTETI", Description: "Departemen Teknik Elektro dan Teknologi Informasi", Latitude: -7.7657, Longitude: 110.3723),
                (Code: "TNTF", Description: "Departemen Teknik Nuklir dan Teknik Fisika", Latitude: -7.7670, Longitude: 110.3718),
                (Code: "DTMI", Description: "Departemen Teknik Mesin dan Industri", Latitude: -7.7659, Longitude: 110.3707),
                (Code: "DTK", Description: "Departemen Teknik Kimia", Latitude: -7.7650, Longitude: 110.3702)
            };
            for (var index = 0; index < definitions.Length; index++)
            {
                var definition = definitions[index];
                var zone = new ParkingZone
                {
                    Name = definition.Code,
                    Description = definition.Description,
                    TotalCapacity = 10,
                    ExternalId = index + 1
                };
                context.Zones.Add(zone);
                for (var slot = 1; slot <= 10; slot++)
                    context.Spots.Add(new ParkingSpot
                    {
                        ZoneId = zone.Id,
                        Name = $"{definition.Code}-{slot:D2}",
                        Latitude = definition.Latitude + (slot % 5) * .000025,
                        Longitude = definition.Longitude + (slot / 5) * .000025,
                        ExternalId = index * 100 + slot,
                        IsOccupied = false
                    });
                context.Cameras.Add(new Camera
                {
                    Name = $"Video {definition.Code}",
                    ZoneId = zone.Id,
                    ConnectionType = "Video",
                    StreamUrl = string.Empty,
                    ExternalId = index + 1
                });
            }
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