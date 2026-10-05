using Microsoft.AspNetCore.SignalR;

namespace Pakar.Api.Hubs;

public class ParkingHub : Hub
{
    // Method ini dipanggil oleh AI Service/Backend saat ada perubahan status
    public async Task BroadcastSpotStatusChange(Guid spotId, bool isOccupied, DateTime timestamp)
    {
        // Kirim ke SEMUA client yang terhubung
        await Clients.All.SendAsync("ReceiveSpotUpdate", new 
        {
            SpotId = spotId,
            IsOccupied = isOccupied,
            UpdatedAt = timestamp
        });
    }
}