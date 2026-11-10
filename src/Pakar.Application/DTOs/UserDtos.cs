namespace Pakar.Application.DTOs;

/// <summary>FR 15: account as shown to admins. The password hash is never exposed.</summary>
public sealed record UserDto(Guid Id, string Name, string Email, string Role, bool IsActive, DateTime CreatedAt);

/// <summary>FR 15: admin edit. Role is "Admin" or "User".</summary>
public sealed record UpdateUserRequest(string Name, string Role);

/// <summary>Signed-in user returned by login, used by the Web app for the "logged in as" display.</summary>
public sealed record AuthUserDto(Guid Id, string Name, string Email, string Role);

/// <summary>Login response: the bearer token, when it expires, and who it belongs to.</summary>
public sealed record LoginResponseDto(string Token, DateTime ExpiresAt, AuthUserDto User);
