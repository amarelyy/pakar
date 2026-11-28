using Microsoft.EntityFrameworkCore;
using Pakar.Application.Common;
using Pakar.Application.DTOs;
using Pakar.Application.Interfaces;
using Pakar.Domain.Entities;
using Pakar.Domain.Services;
using Pakar.Infrastructure.Persistence;

namespace Pakar.Infrastructure.Services;

/// <summary>
/// Reads zone metadata and combines it with the latest observations in memory.
/// </summary>
public sealed class ZoneQueryService(AppDbContext db, IParkingObservations observations, TimeProvider clock) : IZoneQueryService
{
    public const int DefaultSearchLimit = 50;
    public const int MaxSearchLimit = 100;
    public const int MaxQueryLength = 100;

    public async Task<IReadOnlyList<ZoneDto>> ListAsync(CancellationToken ct = default)
    {
        var zones = await db.Zones
            .AsNoTracking()
            .OrderBy(z => z.Name)
            .Select(z => new
            {
                z.Id,
                z.Name,
                z.TotalCapacity,
                z.MapAreaJson,
                z.StatusOverride,
                z.ExternalId,
                Sources = z.Cameras.Select(c => new ZoneSourceDto(c.ExternalId, c.LastActiveTime)).ToList(),
                Spots = z.Spots
                    .OrderBy(s => s.Name)
                    .Select(s => new SpotDto
                    {
                        Id = s.Id,
                        Name = s.Name,
                        Latitude = s.Latitude,
                        Longitude = s.Longitude,
                        IsOccupied = s.IsOccupied,
                        ExternalId = s.ExternalId,
                        LastUpdated = s.LastUpdated
                    }).ToList()
            })
            .ToListAsync(ct);

        var readings = observations.Read();
        var now = clock.GetUtcNow();
        return zones.Select(z =>
        {
            foreach (var spot in z.Spots)
            {
                if (spot.ExternalId is int id && readings.TryGetValue(id, out var reading) &&
                    reading.AreaId == z.ExternalId)
                {
                    spot.ObservedAt = reading.UpdatedAt;
                    spot.ObservedStatus = ObservedOccupancy.StatusAt(reading.Status, reading.UpdatedAt, now);
                }
            }

            var capacity = OccupancyCalculator.EffectiveCapacity(z.TotalCapacity, z.Spots.Count);
            var occupied = z.Spots.Count(spot => spot.ObservedStatus == "occupied");
            var available = z.Spots.Count(spot => spot.ObservedStatus == "available");
            var resolved = ObservedOccupancy.Resolve(capacity, occupied, available, z.StatusOverride);
            return new ZoneDto
            {
                Id = z.Id,
                Name = z.Name,
                TotalCapacity = capacity,
                AvailableSpots = resolved.Available,
                OccupiedSpots = occupied,
                UnknownSpots = resolved.Unknown,
                Status = resolved.Status,
                IsOverridden = z.StatusOverride.HasValue,
                ExternalId = z.ExternalId,
                MapArea = ZoneMapAreaStorage.Deserialize(z.MapAreaJson),
                Spots = z.Spots,
                Sources = z.Sources
            };
        }).ToList();
    }

    public async Task<OperationResult<IReadOnlyList<ZoneSummaryDto>>> SearchAsync(ZoneSearchQuery query, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();

        string? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (string.Equals(query.Status.Trim(), ObservedOccupancy.Unknown, StringComparison.OrdinalIgnoreCase))
                status = ObservedOccupancy.Unknown;
            else if (OccupancyCalculator.TryParseApiValue(query.Status, out var parsed))
                status = parsed.ToApiValue();
            else
                errors[nameof(query.Status)] = ["Status must be available, almost_full, full or unknown."];
        }

        if (query.MinAvailable is < 0)
            errors[nameof(query.MinAvailable)] = ["MinAvailable cannot be negative."];
        if (query.Limit is < 1 or > MaxSearchLimit)
            errors[nameof(query.Limit)] = [$"Limit must be between 1 and {MaxSearchLimit}."];
        if (query.Q is { Length: > MaxQueryLength })
            errors[nameof(query.Q)] = [$"Q must be at most {MaxQueryLength} characters."];
        if (!TryReadPoint(query.Lat, query.Lng, out var origin, out var pointError))
            errors[nameof(query.Lat)] = [pointError!];

        if (errors.Count > 0)
            return OperationResult<IReadOnlyList<ZoneSummaryDto>>.Invalid(errors);

        var zones = db.Zones.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim().ToLowerInvariant();
            var azureSpotPrefix = term switch
            {
                "dtk" => "tk-",
                "dtmi" => "tmi-",
                "dteti" => "teti-",
                "tntf" => "tntf-",
                _ => null
            };
            zones = zones.Where(z => z.Name.ToLower().Contains(term) || z.Description.ToLower().Contains(term) ||
                (azureSpotPrefix != null && z.Spots.Any(spot => spot.Name.ToLower().StartsWith(azureSpotPrefix))));
        }

        var results = (await LoadSummariesAsync(zones, ct))
            .Select(z => WithDistance(z, origin))
            .Where(z => status is null || z.Status == status)
            .Where(z => query.MinAvailable is null || z.AvailableSpots >= query.MinAvailable)
            .OrderBy(z => origin is null ? 0 : z.DistanceMeters ?? double.MaxValue)
            .ThenByDescending(z => z.AvailableSpots)
            .ThenBy(z => z.Name)
            .Take(query.Limit ?? DefaultSearchLimit)
            .ToList();

        return OperationResult<IReadOnlyList<ZoneSummaryDto>>.Ok(results);
    }

    public async Task<OperationResult<AlternateZoneDto>> FindAlternateAsync(Guid zoneId, double? lat, double? lng, CancellationToken ct = default)
    {
        if (!TryReadPoint(lat, lng, out var overridePoint, out var pointError))
            return OperationResult<AlternateZoneDto>.Invalid(nameof(lat), pointError!);

        var zones = await LoadSummariesAsync(db.Zones.AsNoTracking(), ct);
        var source = zones.FirstOrDefault(z => z.Id == zoneId);
        if (source is null)
            return OperationResult<AlternateZoneDto>.NotFound("Zone not found.");

        // Measure from the caller's position when given, otherwise from the requested zone.
        var origin = overridePoint ?? CenterOf(source);

        var alternative = zones
            .Where(z => z.Id != zoneId && OffersSpots(z))
            .Select(z => WithDistance(z, origin))
            .OrderBy(z => origin is null ? 0 : z.DistanceMeters ?? double.MaxValue)
            .ThenByDescending(z => z.AvailableSpots)
            .ThenBy(z => z.Name)
            .FirstOrDefault();

        var isFull = source.Status == ZoneOccupancyStatus.Full.ToApiValue();
        var message = alternative is null
            ? "No other zone has free spots right now."
            : isFull
                ? $"{source.Name} is full. Nearest zone with free spots: {alternative.Name}."
                : $"Nearest other zone with free spots: {alternative.Name}.";

        return OperationResult<AlternateZoneDto>.Ok(
            new AlternateZoneDto(source.Id, source.Name, source.Status, isFull, alternative, message));
    }

    private async Task<List<ZoneSummaryDto>> LoadSummariesAsync(IQueryable<ParkingZone> zones, CancellationToken ct)
    {
        var rows = await zones
            .Select(z => new
            {
                z.Id,
                z.Name,
                z.Description,
                z.TotalCapacity,
                z.StatusOverride,
                z.ExternalId,
                SpotIds = z.Spots.Select(spot => spot.ExternalId).ToList(),
                Latitude = z.Spots.Average(s => (double?)s.Latitude),
                Longitude = z.Spots.Average(s => (double?)s.Longitude)
            })
            .ToListAsync(ct);

        var readings = observations.Read();
        var now = clock.GetUtcNow();
        return rows.Select(r =>
        {
            var statuses = r.SpotIds.Select(id => readings.TryGetValue(id, out var reading) &&
                reading.AreaId == r.ExternalId
                    ? ObservedOccupancy.StatusAt(reading.Status, reading.UpdatedAt, now)
                    : ObservedOccupancy.Unknown).ToList();
            var capacity = OccupancyCalculator.EffectiveCapacity(r.TotalCapacity, r.SpotIds.Count);
            var occupied = statuses.Count(value => value == "occupied");
            var available = statuses.Count(value => value == "available");
            var resolved = ObservedOccupancy.Resolve(capacity, occupied, available, r.StatusOverride);
            return new ZoneSummaryDto(
                r.Id,
                r.Name,
                r.Description,
                capacity,
                occupied,
                resolved.Available,
                resolved.Status,
                r.Latitude,
                r.Longitude,
                DistanceMeters: null,
                IsOverridden: r.StatusOverride.HasValue,
                UnknownSpots: resolved.Unknown);
        }).ToList();
    }

    // A zone the admin forced to "available" is offered even when its (stale) counts show no free spot.
    private static bool OffersSpots(ZoneSummaryDto zone) =>
        zone.Status != ZoneOccupancyStatus.Full.ToApiValue() && (zone.AvailableSpots > 0 || zone.IsOverridden);

    private static ZoneSummaryDto WithDistance(ZoneSummaryDto zone, GeoPoint? origin)
    {
        var center = CenterOf(zone);
        if (origin is null || center is null)
            return zone;
        return zone with { DistanceMeters = Math.Round(GeoDistance.HaversineMeters(origin.Value, center.Value), 1) };
    }

    private static GeoPoint? CenterOf(ZoneSummaryDto zone) =>
        zone is { Latitude: { } latitude, Longitude: { } longitude } ? new GeoPoint(latitude, longitude) : null;

    private static bool TryReadPoint(double? lat, double? lng, out GeoPoint? point, out string? error)
    {
        point = null;
        error = null;
        if (lat is null && lng is null)
            return true;
        if (lat is null || lng is null)
        {
            error = "Lat and Lng must be given together.";
            return false;
        }
        if (!GeoPoint.IsValid(lat.Value, lng.Value))
        {
            error = "Lat must be between -90 and 90 and Lng between -180 and 180.";
            return false;
        }
        point = new GeoPoint(lat.Value, lng.Value);
        return true;
    }
}
