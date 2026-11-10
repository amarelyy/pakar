namespace Pakar.Domain.Entities;

public class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = UserRoles.User;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>FR 15: deactivated accounts cannot log in and their existing tokens are rejected.</summary>
    public bool IsActive { get; set; } = true;

    public bool IsActiveAdmin => IsActive && Role == UserRoles.Admin;

    // Navigation
    public ICollection<Report> Reports { get; set; } = new List<Report>();
    public ICollection<SavedZone> SavedZones { get; set; } = new List<SavedZone>();
}
