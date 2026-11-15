import { MIN_MS, fmtClock } from '@/lib/time'

const STOPS = [15, 30, 45, 60, 90, 120, 180]

/** The arrival-time control. One store value; the map, list and best pick all re-render from it. */
export function PlanScrubber({ now, offsetMin, onChange }: { now: number; offsetMin: number; onChange: (m: number) => void }) {
  const arrival = now + offsetMin * MIN_MS
  return (
    <div className="rounded-card border border-hairline bg-card p-3">
      <div className="flex items-baseline justify-between">
        <label htmlFor="arrival" className="text-xs font-medium text-muted">
          Arriving at
        </label>
        <span className="text-lg font-semibold text-ink tnum">
          {fmtClock(arrival)} <span className="text-xs font-normal text-muted">in {offsetMin} min</span>
        </span>
      </div>
      <input
        id="arrival"
        type="range"
        min={15}
        max={180}
        step={15}
        value={offsetMin}
        onChange={(e) => onChange(Number(e.target.value))}
        className="mt-2 w-full accent-[var(--accent)]"
        aria-valuetext={`${fmtClock(arrival)}, in ${offsetMin} minutes`}
      />
      <div className="mt-1 flex justify-between">
        {STOPS.map((m) => (
          <button
            key={m}
            type="button"
            onClick={() => onChange(m)}
            className={`rounded-chip px-1.5 py-0.5 text-[11px] tnum ${m === offsetMin ? 'bg-ink text-white' : 'text-muted hover:bg-ground'}`}
          >
            {m < 60 ? `${m}m` : `${m / 60}h`}
          </button>
        ))}
      </div>
    </div>
  )
}
