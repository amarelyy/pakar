using System.Data;
using Microsoft.EntityFrameworkCore;
using Pakar.Application.Common;
using Pakar.Application.DTOs;
using Pakar.Application.Interfaces;
using Pakar.Domain.Entities;
using Pakar.Domain.Services;
using Pakar.Infrastructure.Persistence;

namespace Pakar.Infrastructure.Services;

public sealed class UserManagementService(AppDbContext db) : IUserManagementService
{
    public const int MaxNameLength = 100;
    public const int MaxSearchLength = 100;

    public async Task<PagedResult<UserDto>> ListAsync(string? search, bool includeInactive, int? page, int? pageSize, CancellationToken ct = default)
    {
        var query = db.Users.AsNoTracking();
        if (!includeInactive)
            query = query.Where(u => u.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            if (term.Length > MaxSearchLength)
                term = term[..MaxSearchLength];
            query = query.Where(u => u.Name.ToLower().Contains(term) || u.Email.Contains(term));
        }

        var paging = PageRequest.From(page, pageSize);
        var total = await query.CountAsync(ct);
        var users = await query
            .OrderBy(u => u.Name)
            .ThenBy(u => u.Email)
            .Skip(paging.Skip)
            .Take(paging.PageSize)
            .Select(u => new UserDto(u.Id, u.Name, u.Email, u.Role, u.IsActive, u.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<UserDto>(users, paging.Page, paging.PageSize, total);
    }

    public async Task<OperationResult<UserDto>> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        return user is null
            ? OperationResult<UserDto>.NotFound("User not found.")
            : OperationResult<UserDto>.Ok(ToDto(user));
    }

    public async Task<OperationResult<UserDto>> UpdateAsync(Guid actingAdminId, Guid userId, UpdateUserRequest request, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
            errors[nameof(request.Name)] = ["Name is required."];
        else if (name.Length > MaxNameLength)
            errors[nameof(request.Name)] = [$"Name must be at most {MaxNameLength} characters."];

        var role = UserRoles.Normalize(request.Role);
        if (role is null)
            errors[nameof(request.Role)] = [$"Role must be one of: {string.Join(", ", UserRoles.All)}."];

        if (errors.Count > 0)
            return OperationResult<UserDto>.Invalid(errors);

        return await ChangeAsync(actingAdminId, userId,
            removesAccess: user => user.Role == UserRoles.Admin && role != UserRoles.Admin,
            apply: user =>
            {
                user.Name = name;
                user.Role = role!;
            },
            ct);
    }

    public Task<OperationResult<UserDto>> SetActiveAsync(Guid actingAdminId, Guid userId, bool isActive, CancellationToken ct = default) =>
        ChangeAsync(actingAdminId, userId,
            removesAccess: user => user.IsActive && !isActive,
            apply: user => user.IsActive = isActive,
            ct);

    /// <summary>
    /// Applies a change to one user. Changes that remove access (deactivate, demote) are checked
    /// against <see cref="AdminSafeguard"/> inside a serializable transaction, so two admins acting
    /// at the same moment cannot both remove the last other admin.
    /// </summary>
    private async Task<OperationResult<UserDto>> ChangeAsync(
        Guid actingAdminId,
        Guid userId,
        Func<AppUser, bool> removesAccess,
        Action<AppUser> apply,
        CancellationToken ct)
    {
        // Retries (serialization failures are transient) must run inside the execution strategy.
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (user is null)
                return OperationResult<UserDto>.NotFound("User not found.");

            if (removesAccess(user))
            {
                var activeAdmins = await db.Users.CountAsync(u => u.IsActive && u.Role == UserRoles.Admin, ct);
                var refusal = AdminSafeguard.CheckRemoval(user.Id == actingAdminId, user.IsActiveAdmin, activeAdmins);
                if (refusal is not null)
                    return OperationResult<UserDto>.Conflict(refusal);
            }

            apply(user);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return OperationResult<UserDto>.Ok(ToDto(user));
        });
    }

    private static UserDto ToDto(AppUser u) => new(u.Id, u.Name, u.Email, u.Role, u.IsActive, u.CreatedAt);
}
