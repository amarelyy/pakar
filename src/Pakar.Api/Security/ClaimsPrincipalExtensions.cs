using System.Security.Claims;

namespace Pakar.Api.Security;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The signed-in user's id from the JWT subject; throws when the token carries none.</summary>
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new InvalidOperationException("The authenticated principal has no user id claim.");

    public static bool IsAdmin(this ClaimsPrincipal principal) => principal.IsInRole(Roles.Admin);
}
