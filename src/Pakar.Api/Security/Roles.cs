namespace Pakar.Api.Security;

/// <summary>Role names stored on <c>AppUser.Role</c> and emitted as JWT role claims.</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string User = "User";
}

/// <summary>Named authorization policies registered in Program.cs.</summary>
public static class AuthPolicies
{
    public const string AdminOnly = "AdminOnly";
}
