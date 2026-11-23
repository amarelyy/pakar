using Microsoft.EntityFrameworkCore;
using Pakar.Application.Common;
using Pakar.Application.DTOs;
using Pakar.Application.Interfaces;
using Pakar.Application.Validation;
using Pakar.Domain.Entities;
using Pakar.Domain.Services;
using Pakar.Infrastructure.Persistence;

namespace Pakar.Infrastructure.Services;

/// <summary>Stores the admin's manual zone status (FR 17) on the zone row, so it survives restarts.</summary>
public sealed class ZoneOverrideService(AppDbContext db, TimeProvider clock) : IZoneOverrideService
{
    public async Task<IReadOnlyList<ZoneOverrideDto>> ListAsync(CancellationToken ct = default)
    {
        var zones = await db.Zones
            .AsNoTracking()
            .Where(z => z.StatusOverride != null)
            .OrderBy(z => z.Name)
            .ToListAsync(ct);
        return zones.Select(ToDto).ToList();
    }

    public async Task<OperationResult<ZoneOverrideDto>> SetAsync(
        Guid zoneId, Guid adminId, SetZoneOverrideRequest request, CancellationToken ct = default)
    {
        var (status, note, errors) = ZoneOverrideRules.Validate(request);
        if (errors.Count > 0)
            return OperationResult<ZoneOverrideDto>.Invalid(errors);

        var zone = await db.Zones.FirstOrDefaultAsync(z => z.Id == zoneId, ct);
        if (zone is null)
            return OperationResult<ZoneOverrideDto>.NotFound("Zone not found.");

        zone.StatusOverride = status;
        zone.StatusOverrideNote = note;
        zone.StatusOverrideSetAt = clock.GetUtcNow().UtcDateTime;
        zone.StatusOverrideSetBy = await db.Users
            .Where(u => u.Id == adminId)
            .Select(u => u.Name)
            .FirstOrDefaultAsync(ct);

        await db.SaveChangesAsync(ct);
        return OperationResult<ZoneOverrideDto>.Ok(ToDto(zone));
    }

    public async Task<OperationResult<ZoneRefDto>> ClearAsync(Guid zoneId, CancellationToken ct = default)
    {
        var zone = await db.Zones.FirstOrDefaultAsync(z => z.Id == zoneId, ct);
        if (zone is null)
            return OperationResult<ZoneRefDto>.NotFound("Zone not found.");

        if (zone.StatusOverride is not null)
        {
            zone.StatusOverride = null;
            zone.StatusOverrideNote = null;
            zone.StatusOverrideSetAt = null;
            zone.StatusOverrideSetBy = null;
            await db.SaveChangesAsync(ct);
        }

        return OperationResult<ZoneRefDto>.Ok(new ZoneRefDto(zone.Id, zone.Name, zone.ExternalId));
    }

    private static ZoneOverrideDto ToDto(ParkingZone zone) => new(
        zone.Id,
        zone.Name,
        zone.ExternalId,
        zone.StatusOverride!.Value.ToApiValue(),
        zone.StatusOverrideNote,
        zone.StatusOverrideSetAt ?? DateTime.UtcNow,
        zone.StatusOverrideSetBy);
}
