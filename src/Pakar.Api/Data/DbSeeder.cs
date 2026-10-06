using Microsoft.EntityFrameworkCore;
using Pakar.Domain.Entities;
using Pakar.Infrastructure.Persistence;

namespace Pakar.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await context.Database.MigrateAsync();

        if (!await context.Zones.AnyAsync())
        {
            // Zone Gedung H (Teknik Sipil)
            var zoneH = new ParkingZone 
            { 
                Id = Guid.NewGuid(), 
                Name = "Parkir Gedung H (Sipil)", 
                Description = "Area depan Departemen Teknik Sipil dan Lingkungan",
                TotalCapacity = 10,
                ExternalId = 1
            };
            
            // Zone Gedung I (SGLC)
            var zoneI = new ParkingZone 
            { 
                Id = Guid.NewGuid(), 
                Name = "Parkir Gedung I (Pusat)", 
                Description = "Area lobby utama Kantor Pusat Fakultas",
                TotalCapacity = 10,
                ExternalId = 2
            };

            // Zone Barat Gedung E (Geologi)
            var zoneE = new ParkingZone 
            { 
                Id = Guid.NewGuid(), 
                Name = "Parkir Barat Gedung E (Geologi)", 
                Description = "Area 100m ke barat dari Departemen Teknik Geologi",
                TotalCapacity = 10,
                ExternalId = 3
            };

            await context.Zones.AddRangeAsync(zoneH, zoneI, zoneE);
            await context.SaveChangesAsync();

            var spots = new List<ParkingSpot>();

            // Set Slot untuk Gedung H (10 Slot) 
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

            //  Set Slot untuk Gedung I (10 Slot) 
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

            //  Set Slot untuk Gedung E (10 Slot)
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

            // --- Seed Camera (satu per zona untuk mapping AI pipeline) ---
            var cameras = new List<Camera>
            {
                new Camera
                {
                    Name = "CCTV Gedung H",
                    StreamUrl = "rtsp://admin:admin@192.168.1.101:554/stream1",
                    ConnectionType = "IP",
                    ApiKeyHash = "temp-hash",
                    ZoneId = zoneH.Id,
                    ExternalId = 1
                },
                new Camera
                {
                    Name = "CCTV Gedung I",
                    StreamUrl = "rtsp://admin:admin@192.168.1.102:554/stream1",
                    ConnectionType = "IP",
                    ApiKeyHash = "temp-hash",
                    ZoneId = zoneI.Id,
                    ExternalId = 2
                },
                new Camera
                {
                    Name = "CCTV Barat Gedung E",
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
    }
}