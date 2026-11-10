namespace Pakar.Web.Services;

/// <summary>
/// The signed-in account as returned by the API at login. Role is used only to decide what the UI shows;
/// the API enforces every permission itself (FR 9).
/// </summary>
public sealed record SignedInUser(string Token, DateTime ExpiresAt, Guid Id, string Name, string Email, string Role)
{
    public const string AdminRole = "Admin";

    public bool IsAdmin => string.Equals(Role, AdminRole, StringComparison.Ordinal);

    public bool IsExpired(DateTime utcNow) => utcNow >= ExpiresAt;
}
