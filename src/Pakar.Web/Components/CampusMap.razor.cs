using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Pakar.Application.DTOs;
using Pakar.Application.Validation;
using Pakar.Web.Services;

namespace Pakar.Web.Components;

public partial class CampusMap : IAsyncDisposable
{
    [Inject] private IHttpClientFactory HttpClients { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private ILogger<CampusMap> Logger { get; set; } = default!;

    private readonly ParkingMapState state = new();
    private readonly CancellationTokenSource lifetime = new();
    private PeriodicTimer? timer;
    private Task? refreshLoop;
    private DotNetObjectReference<CampusMap>? reference;
    private ElementReference detailHeading;
    private DateTimeOffset now = DateTimeOffset.UtcNow;
    private Guid? selectedId;
    private AlternateZoneDto? alternate;
    private Guid? zoneFilterId;
    private string status = "";
    private int? minimum;
    private int alternateVersion;
    private bool loading = true;
    private bool refreshing, alternateLoading, alternateError;
    private bool mapReady, focusDetail, disposed;
    private string? error, mapError;

    private bool HasFilters => zoneFilterId.HasValue || status.Length > 0 || minimum.HasValue;
    private ZoneDto? Selected => state.Zones.FirstOrDefault(zone => zone.Id == selectedId);
    private IEnumerable<ZoneDto> VisibleZones => state.Zones
        .Where(zone => (!zoneFilterId.HasValue || zone.Id == zoneFilterId) &&
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
                await JS.InvokeVoidAsync("campusMap.init", "campus-map", reference, ZoneMapAreaRules.CampusBounds);
                mapReady = true;
            }
            catch (JSException)
            {
                mapError = "Could not load the map. You can still use the zone list.";
            }

            await RefreshAsync();
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

    private async Task RefreshAsync()
    {
        if (disposed || refreshing)
            return;

        refreshing = true;
        try
        {
            using var client = HttpClients.CreateClient("api");
            var zones = await client.GetFromJsonAsync<List<ZoneDto>>("api/parking/zones", lifetime.Token) ?? [];
            state.Reset(zones, DateTimeOffset.UtcNow);
            if (zoneFilterId is { } id && zones.All(zone => zone.Id != id))
                zoneFilterId = null;
            error = null;
        }
        catch (Exception ex) when (IsConnectionFailure(ex))
        {
            if (disposed)
                return;

            Logger.LogDebug(ex, "Parking data could not be refreshed.");
            error = state.Zones.Count == 0
                ? "Could not load parking zone data."
                : "Could not refresh. Showing the last available data.";
        }
        finally
        {
            refreshing = false;
            loading = false;
        }

        await UpdateViewAsync(refreshAlternate: true);
    }

    private async Task RefreshWhileOpenAsync(PeriodicTimer refreshTimer, CancellationToken ct)
    {
        try
        {
            while (await refreshTimer.WaitForNextTickAsync(ct))
                await RefreshAsync();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    private void ResetFilters()
    {
        zoneFilterId = null;
        status = "";
        minimum = null;
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
        var availabilityChanged = state.Refresh(now);
        if (Selected?.Status != "full")
            ClearAlternate();

        StateHasChanged();
        if (mapReady)
        {
            var zones = state.Zones.Select(zone => new
            {
                zone.Id, zone.Name, zone.Status, zone.AvailableSpots, zone.UnknownSpots, zone.Spots, zone.MapArea,
                WaitingForSource = state.IsWaitingForSource(zone),
                Stale = state.IsStale(zone, now)
            }).ToArray();
            await JS.InvokeVoidAsync("campusMap.renderZones", zones, selectedId?.ToString());
        }

        if (refreshAlternate || availabilityChanged)
            await LoadAlternateAsync();
    }

    private async Task FitMapAsync()
    {
        if (mapReady)
            await JS.InvokeVoidAsync("campusMap.fit");
    }

    private static int Fill(ZoneDto zone) => zone.TotalCapacity <= 0
        ? 0
        : (int)Math.Clamp(zone.OccupiedSpots * 100d / zone.TotalCapacity, 0, 100);

    private static bool IsConnectionFailure(Exception ex) =>
        ex is HttpRequestException or OperationCanceledException or InvalidOperationException or JsonException or TimeoutException or IOException;

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        lifetime.Cancel();
        timer?.Dispose();
        if (refreshLoop is not null)
            await refreshLoop;
        if (mapReady)
            await JS.InvokeVoidAsync("campusMap.dispose");
        reference?.Dispose();
        lifetime.Dispose();
    }
}
