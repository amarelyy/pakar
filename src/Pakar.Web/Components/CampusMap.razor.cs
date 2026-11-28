using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using Pakar.Application.DTOs;
using Pakar.Web.Services;

namespace Pakar.Web.Components;

public partial class CampusMap : IAsyncDisposable
{
    [Inject] private IHttpClientFactory HttpClients { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private ILogger<CampusMap> Logger { get; set; } = default!;

    private readonly ParkingMapState state = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly List<Action> pendingUpdates = [];
    private HubConnection? hub;
    private PeriodicTimer? timer;
    private Task? refreshLoop;
    private DotNetObjectReference<CampusMap>? reference;
    private ElementReference detailHeading;
    private DateTimeOffset now = DateTimeOffset.UtcNow;
    private DateTimeOffset nextMapRefresh = DateTimeOffset.UtcNow;
    private Guid? selectedId;
    private AlternateZoneDto? alternate;
    private HashSet<Guid>? textMatches;
    private string query = "";
    private string status = "";
    private int? minimum;
    private int searchVersion, alternateVersion;
    private bool loading = true;
    private bool connecting, subscribed, syncing, searching, alternateLoading, alternateError;
    private bool mapReady, focusDetail, disposed;
    private string? error, searchError, mapError;

    private bool IsLive => subscribed && hub?.State == HubConnectionState.Connected;
    private bool HasFilters => !string.IsNullOrWhiteSpace(query) || status.Length > 0 || minimum.HasValue;
    private ZoneDto? Selected => state.Zones.FirstOrDefault(zone => zone.Id == selectedId);
    private IEnumerable<ZoneDto> VisibleZones => state.Zones
        .Where(zone => (textMatches is null || textMatches.Contains(zone.Id)) &&
                       (status.Length == 0 || zone.Status == status) &&
                       (!minimum.HasValue || zone.AvailableSpots >= minimum.Value))
        .OrderByDescending(zone => zone.AvailableSpots)
        .ThenBy(zone => zone.Name);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            reference = DotNetObjectReference.Create(this);
            try
            {
                await JS.InvokeVoidAsync("campusMap.init", "campus-map", reference);
                mapReady = true;
            }
            catch (JSException)
            {
                mapError = "Peta belum dapat dimuat. Daftar zona tetap dapat digunakan.";
            }

            await ConnectAsync();
            if (!disposed)
            {
                timer = new PeriodicTimer(TimeSpan.FromSeconds(4));
                refreshLoop = RefreshWhileOpenAsync(timer, lifetime.Token);
            }
        }
        else if (focusDetail && Selected is not null)
        {
            focusDetail = false;
            await detailHeading.FocusAsync();
        }
    }

    private HubConnection CreateHub()
    {
        using var client = HttpClients.CreateClient("api");
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "hubs/parking"))
            .WithAutomaticReconnect()
            .Build();

        connection.On<ParkingSpotUpdate>("SpotStatusChanged",
            update => InvokeAsync(() => ApplyUpdate(() => state.Apply(update))));
        connection.On<ParkingZoneUpdate>("ZoneUpdated", update => InvokeAsync(async () =>
        {
            if (!ApplyUpdate(() => state.Apply(update)))
                return;

            await UpdateViewAsync(refreshAlternate: true);
        }));
        connection.On<ParkingSourceUpdate>("CameraStatusChanged", update => InvokeAsync(async () =>
        {
            if (ApplyUpdate(() => state.Apply(update)))
                await UpdateViewAsync();
        }));
        connection.Reconnecting += _ => InvokeAsync(ShowDisconnected);
        connection.Closed += _ => InvokeAsync(ShowDisconnected);
        connection.Reconnected += _ => InvokeAsync(async () =>
        {
            subscribed = false;
            await SynchronizeAsync(joinHub: true);
        });
        return connection;
    }

    private bool ApplyUpdate(Action update)
    {
        if (disposed)
            return false;
        if (syncing)
        {
            pendingUpdates.Add(update);
            return false;
        }
        if (!IsLive)
            return false;
        update();
        return true;
    }

    private void ShowDisconnected()
    {
        subscribed = false;
        if (!disposed)
            StateHasChanged();
    }

    private async Task ConnectAsync()
    {
        if (connecting || disposed)
            return;

        connecting = true;
        var ct = lifetime.Token;
        try
        {
            hub ??= CreateHub();
            if (hub.State == HubConnectionState.Disconnected)
                await hub.StartAsync(ct);
        }
        catch (Exception ex) when (IsConnectionFailure(ex))
        {
            Logger.LogDebug(ex, "Parking realtime connection could not start.");
        }
        finally
        {
            connecting = false;
        }

        await SynchronizeAsync(joinHub: true);
    }

    private async Task SynchronizeAsync(bool joinHub)
    {
        if (disposed || syncing)
            return;

        syncing = true;
        var ct = lifetime.Token;
        try
        {
            using var client = HttpClients.CreateClient("api");
            var zones = await client.GetFromJsonAsync<List<ZoneDto>>("api/parking/zones", ct) ?? [];
            state.Reset(zones);
            if (joinHub)
            {
                subscribed = false;
                if (state.CanApplyLiveUpdates && hub?.State == HubConnectionState.Connected)
                {
                    foreach (var zone in zones)
                        await hub.InvokeAsync("JoinArea", zone.ExternalId!.Value, ct);

                    state.Apply(await hub.InvokeAsync<ParkingHubSnapshot>("GetSnapshot", ct));
                    subscribed = hub.State == HubConnectionState.Connected;
                }
            }
            error = null;
        }
        catch (Exception ex) when (IsConnectionFailure(ex))
        {
            Logger.LogDebug(ex, "Parking data could not be synchronized.");
            error = state.Zones.Count == 0
                ? "Data zona belum dapat dimuat."
                : "Pembaruan gagal. Menampilkan data terakhir.";
        }
        finally
        {
            foreach (var update in pendingUpdates)
                update();
            pendingUpdates.Clear();
            syncing = false;
            loading = false;
        }

        await UpdateViewAsync(refreshAlternate: true);
    }

    private async Task RefreshWhileOpenAsync(PeriodicTimer refreshTimer, CancellationToken ct)
    {
        try
        {
            while (await refreshTimer.WaitForNextTickAsync(ct))
            {
                if (!IsLive && !connecting)
                    await SynchronizeAsync(joinHub: hub?.State == HubConnectionState.Connected);
                else if (DateTimeOffset.UtcNow >= nextMapRefresh)
                    await UpdateViewAsync();
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    private async Task SearchAsync()
    {
        if (disposed)
            return;

        var version = ++searchVersion;
        var term = query.Trim();
        searchError = null;
        searching = term.Length > 0;
        try
        {
            if (term.Length == 0)
            {
                textMatches = null;
                return;
            }

            using var client = HttpClients.CreateClient("api");
            // Text matching uses the API so descriptions are included. Filter live counts and status locally.
            var path = $"api/parking/zones/search?q={Uri.EscapeDataString(term)}&limit=100";
            var results = await client.GetFromJsonAsync<List<ZoneSummaryDto>>(path, lifetime.Token) ?? [];
            if (version == searchVersion)
                textMatches = results.Select(zone => zone.Id).ToHashSet();
        }
        catch (Exception ex) when (IsConnectionFailure(ex))
        {
            if (version == searchVersion && !disposed)
            {
                searchError = "Pencarian belum berhasil. Coba lagi.";
                textMatches = [];
            }
        }
        finally
        {
            if (version == searchVersion)
                searching = false;
        }
    }

    private void ResetFilters()
    {
        ++searchVersion;
        query = status = "";
        minimum = null;
        textMatches = null;
        searchError = null;
        searching = false;
    }

    [JSInvokable]
    public Task SelectFromMap(string id) => Guid.TryParse(id, out var value)
        ? InvokeAsync(() => SelectAsync(value))
        : Task.CompletedTask;

    private async Task SelectAsync(Guid id)
    {
        if (disposed || state.Zones.All(zone => zone.Id != id))
            return;

        selectedId = id;
        ClearAlternate();
        focusDetail = true;
        if (mapReady)
            await JS.InvokeVoidAsync("campusMap.focusZone", id.ToString());

        await UpdateViewAsync(refreshAlternate: true);
    }

    private async Task CloseDetailAsync()
    {
        selectedId = null;
        focusDetail = false;
        ClearAlternate();
        await UpdateViewAsync();
    }

    private async Task LoadAlternateAsync()
    {
        if (disposed || Selected is not { Status: "full" } selected)
            return;

        var version = ++alternateVersion;
        alternateLoading = true;
        alternateError = false;
        StateHasChanged();
        try
        {
            using var client = HttpClients.CreateClient("api");
            var suggestion = await client.GetFromJsonAsync<AlternateZoneDto>(
                $"api/parking/zones/{selected.Id}/alternate", lifetime.Token);
            if (version == alternateVersion && Selected?.Status == "full")
                alternate = suggestion;
        }
        catch (Exception ex) when (IsConnectionFailure(ex))
        {
            if (version == alternateVersion)
            {
                alternate = null;
                alternateError = true;
            }
        }
        finally
        {
            if (version == alternateVersion)
                alternateLoading = false;
            if (!disposed)
                StateHasChanged();
        }
    }

    private void ClearAlternate()
    {
        ++alternateVersion;
        alternate = null;
        alternateLoading = alternateError = false;
    }

    private async Task UpdateViewAsync(bool refreshAlternate = false)
    {
        if (disposed)
            return;

        now = DateTimeOffset.UtcNow;
        nextMapRefresh = now.AddSeconds(15);
        if (Selected?.Status != "full")
            ClearAlternate();

        StateHasChanged();
        if (mapReady)
        {
            var zones = state.Zones.Select(zone => new
            {
                zone.Id, zone.Name, zone.Status, zone.AvailableSpots, zone.Spots, zone.MapArea,
                Stale = state.IsStale(zone, now)
            }).ToArray();
            await JS.InvokeVoidAsync("campusMap.renderZones", zones, selectedId?.ToString());
        }

        if (refreshAlternate)
            await LoadAlternateAsync();
    }

    private async Task FitMapAsync()
    {
        if (mapReady)
            await JS.InvokeVoidAsync("campusMap.fit");
    }

    private static int Fill(ZoneDto zone) => zone.TotalCapacity <= 0
        ? 0
        : (int)Math.Clamp((zone.TotalCapacity - zone.AvailableSpots) * 100d / zone.TotalCapacity, 0, 100);

    private static bool IsConnectionFailure(Exception ex) =>
        ex is HttpRequestException or OperationCanceledException or InvalidOperationException or JsonException or HubException
            or System.Net.WebSockets.WebSocketException or TimeoutException or IOException;

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        lifetime.Cancel();
        timer?.Dispose();
        if (hub is not null)
            await hub.DisposeAsync();
        if (refreshLoop is not null)
            await refreshLoop;
        if (mapReady)
            await JS.InvokeVoidAsync("campusMap.dispose");
        reference?.Dispose();
        lifetime.Dispose();
    }
}
