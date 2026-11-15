namespace Pakar.Domain.Services;

/// <summary>
/// FR 15 guard rails for user management: an admin cannot lock themselves out,
/// and the system always keeps at least one active admin.
/// </summary>
public static class AdminSafeguard
{
    public const string SelfChangeMessage = "You cannot deactivate your own account or remove your own admin role.";
    public const string LastAdminMessage = "At least one active admin must remain.";

    /// <summary>
    /// Checks a change that deactivates the target or removes its admin role.
    /// Returns the reason it is not allowed, or null when it is.
    /// </summary>
    /// <param name="isSelf">The acting admin is changing their own account.</param>
    /// <param name="targetIsActiveAdmin">The target is currently an active admin.</param>
    /// <param name="activeAdminCount">Active admins in the system, including the target.</param>
    public static string? CheckRemoval(bool isSelf, bool targetIsActiveAdmin, int activeAdminCount)
    {
        if (isSelf)
            return SelfChangeMessage;
        if (targetIsActiveAdmin && activeAdminCount <= 1)
            return LastAdminMessage;
        return null;
    }
}
