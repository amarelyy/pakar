using System.Security.Claims;
using Pakar.Api.Security;
using Pakar.Application.DTOs;
using Pakar.Application.Interfaces;

namespace Pakar.Api.Endpoints;

/// <summary>FR 15: admin user management. Every route requires the Admin role.</summary>
public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        var users = app.MapGroup("/api/users")
            .RequireAuthorization(AuthPolicies.AdminOnly)
            .WithTags("Admin - Users");

        users.MapGet("/", async (string? search, bool? includeInactive, int? page, int? pageSize,
                IUserManagementService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(search, includeInactive ?? true, page, pageSize, ct)));

        users.MapGet("/{id:guid}", async (Guid id, IUserManagementService service, CancellationToken ct) =>
            (await service.GetAsync(id, ct)).ToHttpResult());

        users.MapPut("/{id:guid}", async (Guid id, UpdateUserRequest request, ClaimsPrincipal admin,
                IUserManagementService service, CancellationToken ct) =>
            (await service.UpdateAsync(admin.GetUserId(), id, request, ct)).ToHttpResult());

        users.MapPost("/{id:guid}/deactivate", async (Guid id, ClaimsPrincipal admin,
                IUserManagementService service, CancellationToken ct) =>
            (await service.SetActiveAsync(admin.GetUserId(), id, isActive: false, ct)).ToHttpResult());

        users.MapPost("/{id:guid}/reactivate", async (Guid id, ClaimsPrincipal admin,
                IUserManagementService service, CancellationToken ct) =>
            (await service.SetActiveAsync(admin.GetUserId(), id, isActive: true, ct)).ToHttpResult());
    }
}
