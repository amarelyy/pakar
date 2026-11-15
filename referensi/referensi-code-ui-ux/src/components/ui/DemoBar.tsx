import { ChevronLeft, ChevronRight, Pause, Play, RotateCcw } from 'lucide-react'
import { DEFAULT_START, fmtDate, fmtClock } from '@/lib/time'
import { useSim } from '@/store/useSim'
import { Link } from 'react-router-dom'

/** The visible demo clock. Simulated time; not part of the product surface. */
export function DemoBar({ surface }: { surface: 'driver' | 'ops' }) {
  const now = useSim((s) => s.now)
  const playing = useSim((s) => s.playing)
  const speed = useSim((s) => s.speed)
  const setPlaying = useSim((s) => s.setPlaying)
  const setSpeed = useSim((s) => s.setSpeed)
  const jump = useSim((s) => s.jumpMinutes)
  const setNow = useSim((s) => s.setNow)
  const btn = 'inline-flex h-6 min-w-6 items-center justify-center rounded px-1 text-white/85 hover:bg-white/15 hover:text-white'
  return (
    <div className="flex h-8 shrink-0 items-center gap-1 bg-ink px-2 text-xs text-white/85" role="toolbar" aria-label="Demo clock">
      <span className="hidden rounded bg-white/15 px-1.5 py-0.5 text-[10px] font-semibold uppercase tracking-wide sm:inline">Demo clock</span>
      <span className="ml-1 whitespace-nowrap tnum font-medium text-white">
        <span className="hidden sm:inline">{fmtDate(now)}</span>
        <span className="sm:hidden">{fmtDate(now).slice(0, 3)}</span> {fmtClock(now)}
      </span>
      <div className="ml-auto flex items-center gap-0.5">
        <button type="button" className={btn} onClick={() => jump(-60)} aria-label="Back one hour">
          <ChevronLeft size={14} />
          <span className="hidden sm:inline">1h</span>
        </button>
        <button type="button" className={btn} onClick={() => jump(15)} aria-label="Forward 15 minutes">
          <span className="hidden sm:inline">15m</span>
          <ChevronRight size={14} />
        </button>
        <button type="button" className={btn} onClick={() => jump(60)} aria-label="Forward one hour">
          1h
          <ChevronRight size={14} />
        </button>
        <button type="button" className={btn} onClick={() => setPlaying(!playing)} aria-label={playing ? 'Pause' : 'Play'}>
          {playing ? <Pause size={13} /> : <Play size={13} />}
        </button>
        <select
          aria-label="Speed"
          value={speed}
          onChange={(e) => setSpeed(Number(e.target.value))}
          className="h-6 rounded bg-white/10 px-1 text-xs text-white focus:outline-none"
        >
          <option value={1}>1×</option>
          <option value={10}>10×</option>
          <option value={60}>60×</option>
        </select>
        <button type="button" className={btn} onClick={() => setNow(DEFAULT_START)} aria-label="Reset to Tuesday 08:20">
          <RotateCcw size={13} />
        </button>
        <Link to={surface === 'driver' ? '/ops' : '/'} className="ml-1 whitespace-nowrap rounded border border-white/25 px-2 py-0.5 text-[11px] text-white/90 hover:bg-white/15">
          {surface === 'driver' ? 'Operator' : 'Driver app'}
        </Link>
      </div>
    </div>
  )
}
