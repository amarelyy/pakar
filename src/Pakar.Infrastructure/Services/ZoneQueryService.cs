using Microsoft.EntityFrameworkCore;
using Pakar.Application.Common;
using Pakar.Application.DTOs;
using Pakar.Application.Interfaces;
using Pakar.Domain.Entities;
using Pakar.Domain.Services;
using Pakar.Infrastructure.Persistence;

namespace Pakar.Infrastructure.Services;

/// <summary>
/// Zone availability reads. Every method runs one SQL query that aggregates spot counts per zone;
/// status rules and distances are then applied in memory on the small per-campus zone set.
/// </summary>
public sealed class ZoneQueryService(AppDbContext db) : IZoneQueryService
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
                Spots = z.Spots
                    .OrderBy(s => s.Name)
                    .Select(s => new SpotDto
                    {
                        Id = s.Id,
                        Name = s.Name,
                        Latitude = s.Latitude,
                        Longitude = s.Longitude,
                        IsOccupied = s.IsOccupied
                    }).ToList()
            })
            .ToListAsync(ct);

        return zones.Select(z =>
        {
            var capacity = OccupancyCalculator.EffectiveCapacity(z.TotalCapacity, z.Spots.Count);
            var occupied = z.Spots.Count(s => s.IsOccupied);
            return new ZoneDto
            {
                Id = z.Id,
                Name = z.Name,
                TotalCapacity = capacity,
                AvailableSpots = OccupancyCalculator.Available(capacity, occupied),
                Status = OccupancyCalculator.GetStatus(capacity, occupied).ToApiValue(),
                Spots = z.Spots
            };
        }).ToList();
    }

    public async Task<OperationResult<IReadOnlyList<ZoneSummaryDto>>> SearchAsync(ZoneSearchQuery query, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();

        ZoneOccupancyStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (OccupancyCalculator.TryParseApiValue(query.Status, out var parsed))
                status = parsed;
            else
                errors[nameof(query.Status)] = ["Status must be available, almost_full or full."];
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
            var term = query.Q.Trim().ToLower();
            zones = zones.Where(z => z.Name.ToLower().Contains(term) || z.Description.ToLower().Contains(term));
        }

        var results = (await LoadSummariesAsync(zones, ct))
            .Select(z => WithDistance(z, origin))
            .Where(z => status is null || z.Status == status.Value.ToApiValue())
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
            .Where(z => z.Id != zoneId && z.AvailableSpots > 0)
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
                : $"{source.Name} has free spots. Nearest other zone with free spots: {alternative.Name}.";

        return OperationResult<AlternateZoneDto>.Ok(
            new AlternateZoneDto(source.Id, source.Name, source.Status, isFull, alternative, message));
    }

    // One query: per-zone spot counts and the average spot position as the zone's location.
    private static async Task<List<ZoneSummaryDto>> LoadSummariesAsync(IQueryable<ParkingZone> zones, CancellationToken ct)
    {
        var rows = await zones
            .Select(z => new
            {
                z.Id,
                z.Name,
                z.Description,
                z.TotalCapacity,
                SpotCount = z.Spots.Count(),
                Occupied = z.Spots.Count(s => s.IsOccupied),
                Latitude = z.Spots.Average(s => (double?)s.Latitude),
                Longitude = z.Spots.Average(s => (double?)s.Longitude)
            })
            .ToListAsync(ct);

        return rows.Select(r =>
        {
            var capacity = OccupancyCalculator.EffectiveCapacity(r.TotalCapacity, r.SpotCount);
            return new ZoneSummaryDto(
                r.Id,
                r.Name,
                r.Description,
                capacity,
                r.Occupied,
                OccupancyCalculator.Available(capacity, r.Occupied),
                OccupancyCalculator.GetStatus(capacity, r.Occupied).ToApiValue(),
                r.Latitude,
                r.Longitude,
                DistanceMeters: null);
        }).ToList();
    }

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
