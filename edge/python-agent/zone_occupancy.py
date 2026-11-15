"""Zone summaries and count events, separate from per-spot transitions.

Counts cover the configured ROIs only. Missing evidence is not free capacity.
Pending transitions widen the count range until debounce completes. Only complete,
settled observations establish a count or emit a count-change event.
"""
from __future__ import annotations


class ZoneOccupancyTracker:
    def __init__(self, area_id: int, capacity: int):
        if type(area_id) is not int or type(capacity) is not int or area_id <= 0 or capacity <= 0:
            raise ValueError("Area ID and configured ROI capacity must be positive integers")
        self.area_id = area_id
        self.capacity = capacity
        self.last_complete = None
        self.last_complete_seconds = None
        self.count_changes = 0
        self.invalidate()

    def invalidate(self):
        """Expose unavailable data without erasing the last complete baseline."""
        self.current = self._summary(0, 0, self.capacity)

    def _summary(self, occupied, available, unknown, to_occupied=0, to_available=0):
        pending = to_occupied + to_available
        complete = unknown == 0 and pending == 0
        status = "unknown"
        if complete:
            # Match main's OccupancyCalculator (almost full at 90% occupied).
            status = ("full" if occupied >= self.capacity else
                      "almost_full" if occupied / self.capacity >= .9 else "available")
        return {"areaId": self.area_id, "capacity": self.capacity,
                "occupied": occupied, "available": available, "unknown": unknown,
                "pending": pending, "pendingToOccupied": to_occupied, "pendingToAvailable": to_available,
                "complete": complete, "status": status,
                "occupiedMin": occupied-to_available, "occupiedMax": occupied+unknown+to_occupied}

    def observe(self, counts: dict, seconds: float, pending: dict | None = None):
        occupied, available = counts["occupied"], counts["available"]
        unknown = counts.get("unknown", 0)
        pending = pending or {}
        to_occupied, to_available = pending.get('toOccupied',0), pending.get('toAvailable',0)
        if (counts["areaId"] != self.area_id or counts["capacity"] != self.capacity or
                any(type(n) is not int or n < 0 for n in (occupied, available, unknown,to_occupied,to_available)) or
                to_occupied > available or to_available > occupied or
                occupied + available + unknown != self.capacity):
            raise ValueError("Zone counts must cover each configured ROI exactly once")
        self.current = self._summary(occupied, available, unknown,to_occupied,to_available)
        event = None
        if self.current["complete"]:
            if self.last_complete is None:
                event = {"kind": "zone_snapshot", "media_seconds": seconds,
                         "zone": self.current.copy()}
            elif occupied != self.last_complete["occupied"]:
                event = {"kind": "zone_count_changed", "media_seconds": seconds,
                         "previous_media_seconds": self.last_complete_seconds,
                         "previous_occupied": self.last_complete["occupied"],
                         "zone": self.current.copy()}
                self.count_changes += 1
            self.last_complete = self.current.copy()
            self.last_complete_seconds = seconds
        return self.current.copy(), event
