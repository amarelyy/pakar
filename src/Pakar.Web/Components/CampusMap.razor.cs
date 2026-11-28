using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
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
    private PeriodicTimer? timer;
    private Task? refreshLoop;
    private DotNetObjectReference<CampusMap>? reference;
    private ElementReference detailHeading;
    private DateTimeOffset now = DateTimeOffset.UtcNow;
    private Guid? selectedId;
    private AlternateZoneDto? alternate;
    private HashSet<Guid>? textMatches;
    private string query = "";
    private string status = "";
    private int? minimum;
    private int searchVersion, alternateVersion;
    private bool loading = true;
    private bool refreshing, searching, alternateLoading, alternateError;
    private bool mapReady, focusDetail, disposed;
    private string? error, searchError, mapError;

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
                searchError = "Could not complete the search. Try again.";
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
        var availabilityChanged = state.Refresh(now);
        if (Selected?.Status != "full")
            ClearAlternate();

        StateHasChanged();
        if (mapReady)
        {
            var zones = state.Zones.Select(zone => new
            {
                zone.Id, zone.Name, zone.Status, zone.AvailableSpots, zone.UnknownSpots, zone.Spots,
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
