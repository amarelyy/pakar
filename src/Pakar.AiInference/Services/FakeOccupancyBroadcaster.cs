//A fake broadcaster, so there's something to receive
using Microsoft.AspNetCore.SignalR;
using Pakar.Api.Hubs;
using Pakar.Shared.Dtos;

namespace Pakar.Api.Services;

public class FakeOccupancyBroadcaster(IHubContext<OccupancyHub> hub) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var rnd = new Random();
        while (!stoppingToken.IsCancellationRequested)
        {
            var spots = Enumerable.Range(1, 6).Select(i => new ParkingSpotDto
            {
                SpotId = i,
                DetectedStatus = rnd.Next(2) == 0 ? "available" : "occupied",
                ConfidenceScore = Math.Round(rnd.NextDouble(), 2)
            }).ToList();

            await hub.Clients.All.SendAsync("ReceiveOccupancyUpdate", spots, stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }
}
