namespace Pakar.Domain.Entities;

public class Report
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // Pending, Reviewed, Resolved
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public string? EvidenceImageUrl { get; set; }

    // Foreign Keys
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    
    public Guid SpotId { get; set; }
    public ParkingSpot Spot { get; set; } = null!;
}// [RESTORED] Entity for reports