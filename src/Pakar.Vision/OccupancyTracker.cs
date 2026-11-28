using Pakar.Shared.Dtos;

namespace Pakar.Vision;

public sealed record StableSlot(int SpotId, string SpotNumber, string Status, double Confidence);
public sealed record OccupancyResult(List<StableSlot> Visible, List<StableSlot> Changes,
    Dictionary<int, string> Unknown, int Pending, int Occupied, int Available, int Capacity,
    int CoveredSlots, int OccupiedMin, int OccupiedMax, List<StableSlot> Confirmed);

/// <summary>Use monotonic elapsed seconds, never video position or UTC, for debounce.</summary>
public sealed class OccupancyTracker
{
    private sealed class State
    {
        public string? Status, Candidate;
        public double Since, Confidence;
        public bool RequiresVacancyConfirmation;
    }
    private readonly List<WorkerSlotDto> slots;
    private readonly Dictionary<int, State> states;
    private readonly double occupiedDelay, availableDelay, availableConfirmationConfidence;
    private double lastSeconds = double.NegativeInfinity;

    public OccupancyTracker(List<WorkerSlotDto> slots, double occupiedDelay = 2, double availableDelay = 3,
        double availableConfirmationConfidence = 0)
    {
        if (!double.IsFinite(occupiedDelay) || !double.IsFinite(availableDelay) || occupiedDelay < 0 || availableDelay < 0)
            throw new ArgumentException("Invalid debounce.");
        if (!double.IsFinite(availableConfirmationConfidence) || availableConfirmationConfidence is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(availableConfirmationConfidence));

        this.slots = slots;
        this.occupiedDelay = occupiedDelay;
        this.availableDelay = availableDelay;
        this.availableConfirmationConfidence = availableConfirmationConfidence;
        states = slots.ToDictionary(s => s.SpotId, _ => new State());
    }

    public void ResetPending()
    {
        foreach (var state in states.Values) state.Candidate = null;
    }

    public OccupancyResult Update(IReadOnlyDictionary<int, Observation> observations, double seconds, int? zoneCapacity = null)
    {
        if (!double.IsFinite(seconds)) throw new ArgumentException("Invalid monotonic clock.");
        if (seconds < lastSeconds) ResetPending();
        lastSeconds = seconds;
        int capacity = zoneCapacity ?? slots.Count;
        if (capacity < slots.Count) throw new ArgumentException("Capacity is below ROI coverage.");
        var visible = new List<StableSlot>();
        var changes = new List<StableSlot>();
        var confirmed = new List<StableSlot>();
        var unknown = new Dictionary<int, string>(); int pending = 0, minimum = 0, maximum = capacity - slots.Count;
        foreach (var slot in slots)
        {
            var state = states[slot.SpotId];
            if (!observations.TryGetValue(slot.SpotId, out var observed) || observed.Status is not ("occupied" or "available"))
            {
                state.Candidate = null;
                unknown[slot.SpotId] = observed?.Reason ?? "source_gap"; maximum++;
                continue;
            }
            string? previous = state.Status;
            if (state.Status is null)
            {
                state.Status = state.Candidate = observed.Status; state.Since = seconds;
            }
            else
            {
                if (state.Candidate != observed.Status) { state.Candidate = observed.Status; state.Since = seconds; }
                double delay = observed.Status == "occupied" ? occupiedDelay : availableDelay;
                if (state.Status != observed.Status && seconds - state.Since >= delay) state.Status = observed.Status;
            }
            bool isPending = state.Status != observed.Status;
            if (isPending) { pending++; maximum++; }
            else { state.Confidence = observed.Confidence; if (state.Status == "occupied") { minimum++; maximum++; } }
            var item = new StableSlot(slot.SpotId, slot.SpotNumber, state.Status!, state.Confidence);
            visible.Add(item);
            if (!isPending)
            {
                if (item.Status == "occupied")
                {
                    state.RequiresVacancyConfirmation = true;
                    confirmed.Add(item);
                }
                else if (!state.RequiresVacancyConfirmation || observed.Confidence >= availableConfirmationConfidence)
                {
                    // Releasing a previously occupied slot needs stronger empty evidence than a first reading.
                    state.RequiresVacancyConfirmation = false;
                    confirmed.Add(item);
                }
            }
            if (previous != state.Status) changes.Add(item);
        }
        return new(visible, changes, unknown, pending, visible.Count(s => s.Status == "occupied"),
            visible.Count(s => s.Status == "available"), capacity, slots.Count, minimum, maximum, confirmed);
    }
}
