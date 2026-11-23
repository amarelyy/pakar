using Microsoft.EntityFrameworkCore;
using Pakar.Application.Common;
using Pakar.Application.DTOs;
using Pakar.Application.Interfaces;
using Pakar.Application.Validation;
using Pakar.Infrastructure.Persistence;

namespace Pakar.Infrastructure.Services;

/// <summary>
/// Reads the stored detections. The per-zone series is one grouped SQL query (date_trunc on the timestamp index),
/// so it stays cheap however many detections exist; the raw list is paged.
/// </summary>
public sealed class DetectionHistoryService(AppDbContext db, TimeProvider clock) : IDetectionHistoryService
{
    public async Task<OperationResult<ZoneHistoryDto>> GetZoneSeriesAsync(Guid zoneId, ZoneHistoryQuery query, CancellationToken ct = default)
    {
        var (window, errors) = HistoryRules.ResolveSeries(query, clock.GetUtcNow().UtcDateTime);
        if (window is null)
            return OperationResult<ZoneHistoryDto>.Invalid(errors);

        // The zone query filter hides removed zones, so their history is not served publicly.
        var zone = await db.Zones.AsNoTracking()
            .Where(z => z.Id == zoneId)
            .Select(z => new { z.Id, z.Name })
            .FirstOrDefaultAsync(ct);
        if (zone is null)
            return OperationResult<ZoneHistoryDto>.NotFound("Zone not found.");

        var rows = db.Detections.AsNoTracking()
            .Where(d => d.SpotId != null && d.Spot!.ZoneId == zoneId && d.Timestamp >= window.From && d.Timestamp < window.To);

        // Day and hour keys translate to date_trunc / date_part in PostgreSQL (UTC session), so grouping stays in SQL.
        var buckets = window.Bucket == HistoryBucket.Day
            ? await rows
                .GroupBy(d => new { d.Timestamp.Date, Hour = 0 })
                .Select(g => new BucketRow(g.Key.Date, g.Key.Hour, g.Count(), g.Count(d => d.Status == "occupied"), g.Select(d => d.SpotId).Distinct().Count()))
                .ToListAsync(ct)
            : await rows
                .GroupBy(d => new { d.Timestamp.Date, d.Timestamp.Hour })
                .Select(g => new BucketRow(g.Key.Date, g.Key.Hour, g.Count(), g.Count(d => d.Status == "occupied"), g.Select(d => d.SpotId).Distinct().Count()))
                .ToListAsync(ct);

        var points = buckets
            .OrderBy(b => b.Date).ThenBy(b => b.Hour)
            .Select(b => new ZoneHistoryPointDto(
                DateTime.SpecifyKind(b.Date, DateTimeKind.Utc).AddHours(b.Hour),
                b.Observations,
                b.Occupied,
                Math.Round(b.Occupied / (double)b.Observations, 4),
                b.Spots))
            .ToList();

        return OperationResult<ZoneHistoryDto>.Ok(
            new ZoneHistoryDto(zone.Id, zone.Name, window.Bucket.ToApiValue(), window.From, window.To, points));
    }

    public async Task<OperationResult<PagedResult<DetectionRecordDto>>> ListRecordsAsync(DetectionHistoryQuery query, CancellationToken ct = default)
    {
        var (window, errors) = HistoryRules.ResolveRecords(query.From, query.To, clock.GetUtcNow().UtcDateTime);
        if (!string.IsNullOrWhiteSpace(query.Status) && !HistoryRules.IsValidStatus(query.Status))
            errors[nameof(query.Status)] = ["Status must be occupied or available."];
        if (errors.Count > 0 || window is null)
            return OperationResult<PagedResult<DetectionRecordDto>>.Invalid(errors);

        var paging = PageRequest.From(query.Page, query.PageSize);

        // Records of removed cameras and zones stay readable for admins, so the query filters are skipped.
        var rows = db.Detections.AsNoTracking().IgnoreQueryFilters()
            .Where(d => d.Timestamp >= window.From && d.Timestamp < window.To);
        if (query.ZoneId is { } zoneId)
            rows = rows.Where(d => d.Spot != null && d.Spot.ZoneId == zoneId);
        if (query.CameraId is { } cameraId)
            rows = rows.Where(d => d.CameraId == cameraId);
        if (query.SpotExternalId is { } spotExternalId)
            rows = rows.Where(d => d.SpotExternalId == spotExternalId);
        if (!string.IsNullOrWhiteSpace(query.Status))
            rows = rows.Where(d => d.Status == query.Status);

        var total = await rows.CountAsync(ct);
        var items = await rows
            .OrderByDescending(d => d.Timestamp)
            .Skip(paging.Skip)
            .Take(paging.PageSize)
            .Select(d => new DetectionRecordDto(
                d.Id,
                d.Timestamp,
                d.Status,
                d.ConfidenceScore,
                d.SpotExternalId,
                d.SpotId,
                d.Spot != null ? d.Spot.Name : null,
                d.Spot != null ? d.Spot.ZoneId : null,
                d.CameraId,
                d.Camera.Name))
            .ToListAsync(ct);

        return OperationResult<PagedResult<DetectionRecordDto>>.Ok(new PagedResult<DetectionRecordDto>(items, paging.Page, paging.PageSize, total));
    }

    private sealed record BucketRow(DateTime Date, int Hour, int Observations, int Occupied, int Spots);
}
