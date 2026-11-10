using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Pakar.Infrastructure.Persistence;

namespace Pakar.Api.Security;

/// <summary>
/// Runs after a JWT passes signature and lifetime checks. Rejects tokens of accounts that were
/// deactivated (FR 15) or whose role changed since login (FR 9), so admin changes apply at once
/// instead of when the token expires. Costs one primary-key lookup per authenticated request.
/// </summary>
public static class ActiveAccountValidator
{
    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        if (!Guid.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            context.Fail("Token has no user id.");
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var account = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.IsActive, u.Role })
            .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

        if (account is null || !account.IsActive)
            context.Fail("Account is deactivated or no longer exists.");
        else if (!principal!.IsInRole(account.Role))
            context.Fail("Account role changed; log in again.");
    }
}
