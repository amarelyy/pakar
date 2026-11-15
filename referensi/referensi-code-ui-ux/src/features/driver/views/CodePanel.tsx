import { useState } from 'react'
import { Button } from '@/components/ui/Button'
import { Panel } from '@/components/ui/Panel'
import { ZONES } from '@/sim/zones'
import { useSim } from '@/store/useSim'

export function CodePanel({ onBack, onSubmit }: { onBack: () => void; onSubmit: (code: string) => boolean }) {
  const [code, setCode] = useState('')
  const [error, setError] = useState<string | null>(null)
  const recent = useSim((s) => s.recentCodes)
  const addRecent = useSim((s) => s.addRecentCode)
  function submit(c: string) {
    const ok = onSubmit(c)
    if (ok) addRecent(c.toUpperCase())
    else setError(`No zone with code ${c.toUpperCase()}. Codes look like A1 or P2.`)
  }
  return (
    <Panel title="Zone code" onBack={onBack}>
      <p className="text-sm text-muted">Type the code printed on the sign at the entrance.</p>
      <form
        className="mt-3 flex gap-2"
        onSubmit={(e) => {
          e.preventDefault()
          if (code.trim()) submit(code.trim())
        }}
      >
        <input
          value={code}
          onChange={(e) => {
            setCode(e.target.value)
            setError(null)
          }}
          placeholder="e.g. A2"
          aria-label="Zone code"
          autoFocus
          className="h-12 flex-1 rounded-card border border-hairline bg-card px-3 text-lg font-semibold uppercase tracking-wider outline-none placeholder:font-normal placeholder:normal-case placeholder:tracking-normal focus:border-accent"
        />
        <Button variant="primary" size="lg" type="submit" disabled={!code.trim()}>
          Open
        </Button>
      </form>
      {error && <p className="mt-2 text-sm text-bad-ink">{error}</p>}
      {recent.length > 0 && (
        <div className="mt-5">
          <div className="mb-1.5 text-xs font-medium text-muted">Recent</div>
          <div className="flex flex-wrap gap-2">
            {recent.map((c) => (
              <button key={c} type="button" onClick={() => submit(c)} className="rounded-chip border border-hairline bg-card px-3 py-1.5 text-sm font-medium hover:bg-ground">
                {c}
              </button>
            ))}
          </div>
        </div>
      )}
      <div className="mt-5">
        <div className="mb-1.5 text-xs font-medium text-muted">All codes</div>
        <div className="flex flex-wrap gap-2">
          {ZONES.map((z) => (
            <button key={z.id} type="button" onClick={() => submit(z.code)} className="rounded-chip border border-hairline bg-card px-3 py-1.5 text-sm hover:bg-ground">
              <span className="font-medium">{z.code}</span> <span className="text-muted">{z.shortName}</span>
            </button>
          ))}
        </div>
      </div>
    </Panel>
  )
}
