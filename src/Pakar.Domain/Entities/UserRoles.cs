namespace Pakar.Domain.Entities;

/// <summary>Role names stored on <see cref="AppUser.Role"/> (FR 9).</summary>
public static class UserRoles
{
    public const string Admin = "Admin";
    public const string User = "User";

    public static IReadOnlyList<string> All { get; } = [Admin, User];

    /// <summary>Returns the canonical role name for case-insensitive input, or null when unknown.</summary>
    public static string? Normalize(string? role) =>
        All.FirstOrDefault(r => string.Equals(r, role?.Trim(), StringComparison.OrdinalIgnoreCase));
}
