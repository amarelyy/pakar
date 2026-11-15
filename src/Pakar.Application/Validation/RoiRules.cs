using Pakar.Shared.Dtos;
using Pakar.Shared.Geometry;

namespace Pakar.Application.Validation;

public static class RoiRules
{
    public static Dictionary<string, string[]> Validate(RoiRequest request, IReadOnlySet<Guid> zoneSpots)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Slots is null || request.Slots.Count > 500)
        {
            errors["Slots"] = ["Supply 0 to 500 slots."]; return errors;
        }
        var seen = new HashSet<Guid>();
        for (int i = 0; i < request.Slots.Count; i++)
        {
            var slot = request.Slots[i];
            if (slot is null) { errors[$"Slots[{i}]"] = ["Slot is required."]; continue; }
            if (!zoneSpots.Contains(slot.SpotId)) errors[$"Slots[{i}].SpotId"] = ["Spot must exist in this source's zone."];
            if (!seen.Add(slot.SpotId)) errors[$"Slots[{i}].SpotId"] = ["Duplicate spot ID."];
            var rounded = slot.Points?.Select(p => p?.Select(v => Math.Round(v, 6)).ToArray()!).ToArray();
            var error = PolygonRules.Validate(rounded);
            if (error is not null) errors[$"Slots[{i}].Points"] = [error];
        }
        return errors;
    }
}
