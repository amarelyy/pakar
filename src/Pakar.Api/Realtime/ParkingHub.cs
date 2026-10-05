using Microsoft.AspNetCore.SignalR;

namespace Pakar.Api.Realtime;

/// <summary>
/// Hub SignalR untuk frontend. Guest (belum login) boleh connect — sesuai FR3.
/// Server -> client events:
///   SpotStatusChanged(SpotView)    : 1 slot berubah status
///   ZoneUpdated(ZoneView)          : okupansi zona berubah (juga dipakai untuk alert "penuh")
///   CameraStatusChanged(CameraView): kamera aktif/inactive
/// </summary>
public class ParkingHub : Hub
{
    public const string AllGroup = "all";
    public static string AreaGroup(int areaId) => $"area-{areaId}";

    private readonly ParkingStateStore _store;
    public ParkingHub(ParkingStateStore store) => _store = store;

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, AllGroup);
        await base.OnConnectedAsync();
    }

    // Client bisa subscribe ke zona tertentu (mis. halaman detail zona)
    public Task JoinArea(int areaId) => Groups.AddToGroupAsync(Context.ConnectionId, AreaGroup(areaId));
    public Task LeaveArea(int areaId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, AreaGroup(areaId));

    // Dipanggil client saat pertama connect / setelah reconnect untuk sinkronisasi state
    public ParkingSnapshot GetSnapshot() => _store.GetSnapshot();
}
