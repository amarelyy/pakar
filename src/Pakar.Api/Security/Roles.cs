using Pakar.Domain.Entities;

namespace Pakar.Api.Security;

/// <summary>Role names emitted as JWT role claims; the values live in <see cref="UserRoles"/>.</summary>
public static class Roles
{
    public const string Admin = UserRoles.Admin;
    public const string User = UserRoles.User;
}

/// <summary>Named authorization policies registered in Program.cs.</summary>
public static class AuthPolicies
{
    public const string AdminOnly = "AdminOnly";
}
