namespace Pakar.Domain.Services;

/// <summary>
/// Validation rules for zones and video sources (FR 13, 14). Pure functions so they can be unit tested
/// without a database. Each method returns field errors keyed by request property name.
/// </summary>
public static class FacilityRules
{
    public const int MaxZoneNameLength = 100;
    public const int MaxDescriptionLength = 500;
    public const int MaxZoneCapacity = 500;
    public const int MaxCameraNameLength = 100;
    public const int MaxVideoFileLength = 255;

    /// <summary>The video source is a file replayed by the worker, so only common video containers are accepted.</summary>
    public static IReadOnlyList<string> AllowedVideoExtensions { get; } = [".mp4", ".avi", ".mov", ".mkv", ".webm"];

    /// <param name="existingSpotCount">Spots already in the zone (0 for a new zone); capacity cannot go below it.</param>
    public static Dictionary<string, string[]> ValidateZone(string? name, string? description, int totalCapacity, int existingSpotCount)
    {
        var errors = new Dictionary<string, string[]>();

        var trimmedName = name?.Trim() ?? string.Empty;
        if (trimmedName.Length == 0)
            errors["Name"] = ["Name is required."];
        else if (trimmedName.Length > MaxZoneNameLength)
            errors["Name"] = [$"Name must be at most {MaxZoneNameLength} characters."];

        if ((description?.Trim().Length ?? 0) > MaxDescriptionLength)
            errors["Description"] = [$"Description must be at most {MaxDescriptionLength} characters."];

        if (totalCapacity < 1 || totalCapacity > MaxZoneCapacity)
            errors["TotalCapacity"] = [$"Capacity must be between 1 and {MaxZoneCapacity}."];
        else if (totalCapacity < existingSpotCount)
            errors["TotalCapacity"] = [$"Capacity cannot be lower than the {existingSpotCount} spots already in this zone."];

        return errors;
    }

    public static Dictionary<string, string[]> ValidateCamera(string? name, string? videoFile)
    {
        var errors = new Dictionary<string, string[]>();

        var trimmedName = name?.Trim() ?? string.Empty;
        if (trimmedName.Length == 0)
            errors["Name"] = ["Name is required."];
        else if (trimmedName.Length > MaxCameraNameLength)
            errors["Name"] = [$"Name must be at most {MaxCameraNameLength} characters."];

        var videoError = ValidateVideoFile(videoFile);
        if (videoError is not null)
            errors["VideoFile"] = [videoError];

        return errors;
    }

    /// <summary>
    /// The video file is optional (it can be set later). When given it must be a plain file name:
    /// no folders and no ".." so it can only point inside the configured video folder.
    /// </summary>
    public static string? ValidateVideoFile(string? videoFile)
    {
        var file = videoFile?.Trim();
        if (string.IsNullOrEmpty(file))
            return null;

        if (file.Length > MaxVideoFileLength)
            return $"Video file name must be at most {MaxVideoFileLength} characters.";

        if (file.IndexOfAny(['/', '\\', ':']) >= 0 || file.Contains(".."))
            return "Video file must be a file name only, without folders.";

        var extension = Path.GetExtension(file);
        if (!AllowedVideoExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            return $"Video file must end with one of: {string.Join(", ", AllowedVideoExtensions)}.";

        return null;
    }

    /// <summary>Stored value for the video file: trimmed, empty when none was given.</summary>
    public static string NormalizeVideoFile(string? videoFile) => videoFile?.Trim() ?? string.Empty;
}
