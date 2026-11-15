using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Pakar.Api.Security;
using Pakar.Application.DTOs;
using Pakar.Domain.Entities;
using Pakar.Infrastructure.Persistence;

namespace Pakar.Api.Controllers;

/// <summary>Account registration and JWT login (FR 7, 8).</summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    public const string DeactivatedMessage = "This account has been deactivated. Contact an administrator.";

    private readonly AppDbContext _context;
    private readonly JwtOptions _jwt;
    private readonly TimeProvider _clock;

    public AuthController(AppDbContext context, IOptions<JwtOptions> jwt, TimeProvider clock)
    {
        _context = context;
        _jwt = jwt.Value;
        _clock = clock;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var email = EmailAddresses.Normalize(request.Email);
        if (await _context.Users.AnyAsync(u => u.Email == email))
            return BadRequest("Email already registered.");

        // Self-registration always yields a regular user; admins are provisioned via DbSeeder.
        var user = new AppUser
        {
            Name = request.Name.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = Roles.User
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Registration successful" });
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequest request)
    {
        var email = EmailAddresses.Normalize(request.Email);
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized("Invalid credentials.");

        // FR 15: checked after the password so the response does not reveal which emails exist.
        if (!user.IsActive)
            return Problem(DeactivatedMessage, statusCode: StatusCodes.Status403Forbidden, title: "Account deactivated");

        var expiresAt = _clock.GetUtcNow().UtcDateTime.AddMinutes(_jwt.ExpiryMinutes);
        return Ok(new LoginResponseDto(
            GenerateJwtToken(user, expiresAt),
            expiresAt,
            new AuthUserDto(user.Id, user.Name, user.Email, user.Role)));
    }

    private string GenerateJwtToken(AppUser user, DateTime expiresAt)
    {
        var creds = new SigningCredentials(_jwt.GetSigningKey(), SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(ClaimTypes.Email, user.Email)
        };

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public record RegisterRequest(
    [Required, MaxLength(100)] string Name,
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MinLength(8), MaxLength(128)] string Password);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);
