using Pakar.Application.Common;
using Pakar.Application.DTOs;

namespace Pakar.Application.Interfaces;

/// <summary>FR 15: admin user management. Every change goes through <c>AdminSafeguard</c>.</summary>
public interface IUserManagementService
{
    /// <summary>Search matches name or email. Inactive users are included unless excluded.</summary>
    Task<PagedResult<UserDto>> ListAsync(string? search, bool includeInactive, int? page, int? pageSize, CancellationToken ct = default);

    Task<OperationResult<UserDto>> GetAsync(Guid userId, CancellationToken ct = default);

    Task<OperationResult<UserDto>> UpdateAsync(Guid actingAdminId, Guid userId, UpdateUserRequest request, CancellationToken ct = default);

    Task<OperationResult<UserDto>> SetActiveAsync(Guid actingAdminId, Guid userId, bool isActive, CancellationToken ct = default);
}
