using Pakar.Application.DTOs;
using Pakar.Domain.Services;

namespace Pakar.Application.Validation;

/// <summary>Validation of the manual zone status request (FR 17).</summary>
public static class ZoneOverrideRules
{
    public const int MaxNoteLength = 200;

    /// <summary>Returns the parsed status and trimmed note, or the field errors.</summary>
    public static (ZoneOccupancyStatus? Status, string? Note, Dictionary<string, string[]> Errors) Validate(SetZoneOverrideRequest? request)
    {
        var errors = new Dictionary<string, string[]>();
        ZoneOccupancyStatus? status = null;

        if (request is null || string.IsNullOrWhiteSpace(request.Status))
            errors[nameof(SetZoneOverrideRequest.Status)] = ["Status is required: available, almost_full or full."];
        else if (OccupancyCalculator.TryParseApiValue(request.Status, out var parsed))
            status = parsed;
        else
            errors[nameof(SetZoneOverrideRequest.Status)] = ["Status must be available, almost_full or full."];

        var note = request?.Note?.Trim();
        if (string.IsNullOrEmpty(note))
            note = null;
        else if (note.Length > MaxNoteLength)
            errors[nameof(SetZoneOverrideRequest.Note)] = [$"Note must be at most {MaxNoteLength} characters."];

        return (status, note, errors);
    }
}
