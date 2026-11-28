using System.Data;
using Microsoft.EntityFrameworkCore;
using Pakar.Domain.Entities;
using Pakar.Infrastructure.Persistence;
using Pakar.Infrastructure.Services;

namespace Pakar.Api.Data;

internal static class ParkingSeed
{
    private const int SlotsPerZone = 10;

    // Reference points from the team's four-zone demo. Capacity and slot positions are demo data.
    private static readonly (string Code, string Department, double Latitude, double Longitude)[] Zones =
    [
        ("DTETI", "Departemen Teknik Elektro dan Teknologi Informasi", -7.7657, 110.3723),
        ("TNTF", "Departemen Teknik Nuklir dan Teknik Fisika", -7.7670, 110.3718),
        ("DTMI", "Departemen Teknik Mesin dan Industri", -7.7659, 110.3707),
        ("DTK", "Departemen Teknik Kimia", -7.7650, 110.3702)
    ];

    private static readonly string[] LegacyZoneNames =
    [
        "Building H Parking (Civil Engineering)",
        "Building I Parking (Faculty Office)",
        "Building E West Parking (Geological Engineering)"
    ];

    public static async Task ApplyAsync(AppDbContext context, IConfiguration config, ILogger logger)
    {
        var replaceLegacyDemo = config.GetValue<bool>("ParkingSeed:ReplaceLegacyDemo");
        var execution = context.Database.CreateExecutionStrategy();

        // Read and update the layout together. A concurrent seed or admin edit can force a retry.
        var changed = await execution.ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var hasZones = await context.Zones.IgnoreQueryFilters().AnyAsync();

            if (hasZones)
            {
                if (!replaceLegacyDemo)
                    return false;

                var activeZones = await context.Zones.Include(zone => zone.Cameras)
                    .OrderBy(zone => zone.ExternalId).ToListAsync();

                if (!IsLegacyDemo(activeZones))
                {
                    logger.LogInformation("Skipped demo parking replacement because the active layout does not match the original seed.");
                    return false;
                }

                // Follow the admin delete behavior and preserve IDs and rows used by historical data.
                var now = DateTime.UtcNow;
                foreach (var zone in activeZones)
                {
                    zone.DeletedAt = now;
                    foreach (var camera in zone.Cameras)
                        camera.DeletedAt = now;
                }
            }

            var nextZoneId = (await context.Zones.IgnoreQueryFilters().MaxAsync(zone => (int?)zone.ExternalId) ?? 0) + 1;
            var nextCameraId = (await context.Cameras.IgnoreQueryFilters().MaxAsync(camera => (int?)camera.ExternalId) ?? 0) + 1;
            var firstSpotId = (await context.Spots.IgnoreQueryFilters().MaxAsync(spot => (int?)spot.ExternalId) ?? 0) + 1;

            for (var zoneIndex = 0; zoneIndex < Zones.Length; zoneIndex++)
            {
                var definition = Zones[zoneIndex];
                var zone = new ParkingZone
                {
                    Name = definition.Code,
                    Description = $"{definition.Department}. Demo layout with approximate slot coordinates.",
                    TotalCapacity = SlotsPerZone,
                    ExternalId = nextZoneId + zoneIndex
                };

                for (var slotIndex = 0; slotIndex < SlotsPerZone; slotIndex++)
                {
                    const double spacing = 0.000025;
                    const int slotsPerRow = 5;
                    zone.Spots.Add(new ParkingSpot
                    {
                        Name = $"{definition.Code}-{slotIndex + 1:D2}",
                        Latitude = definition.Latitude + (slotIndex / slotsPerRow) * spacing,
                        Longitude = definition.Longitude + (slotIndex % slotsPerRow) * spacing,
                        ExternalId = firstSpotId + zoneIndex * 100 + slotIndex
                    });
                }

                zone.Cameras.Add(new Camera
                {
                    Name = $"Video {definition.Code}",
                    ConnectionType = FacilityAdminService.VideoConnectionType,
                    ApiKeyHash = "temp-hash",
                    ExternalId = nextCameraId + zoneIndex
                });
                context.Zones.Add(zone);
            }

            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        });

        if (changed)
            logger.LogInformation("Seeded {ZoneCount} demo parking zones and {SpotCount} slots.", Zones.Length, Zones.Length * SlotsPerZone);
    }

    private static bool IsLegacyDemo(IReadOnlyList<ParkingZone> activeZones)
    {
        if (activeZones.Count != LegacyZoneNames.Length)
            return false;

        for (var index = 0; index < activeZones.Count; index++)
        {
            var zone = activeZones[index];
            if (zone.ExternalId != index + 1 ||
                zone.Name != LegacyZoneNames[index] ||
                zone.TotalCapacity != 10 ||
                zone.Cameras.Count != 1 ||
                zone.Cameras.Single().ExternalId != zone.ExternalId)
                return false;
        }

        return true;
    }
}
