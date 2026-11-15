import { Panel } from '@/components/ui/Panel'
import { StatusGlyph } from '@/components/ui/StatusChip'
import { HOW_WE_PICK, STATUS_LABEL, THRESHOLD_LINES } from '@/copy'
import type { StatusLevel } from '@/domain/types'

const ORDER: StatusLevel[] = ['available', 'limited', 'almostFull', 'full', 'closed', 'noData']

export function HowWePickPanel({ onBack }: { onBack: () => void }) {
  return (
    <Panel title={HOW_WE_PICK.title} onBack={onBack}>
      <div className="space-y-5 text-sm leading-6 text-ink">
        <p>{HOW_WE_PICK.intro}</p>
        <ol className="space-y-3">
          {HOW_WE_PICK.factors.map(([title, body], i) => (
            <li key={title} className="flex gap-3">
              <span className="mt-0.5 inline-flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-ink text-xs font-semibold text-white">{i + 1}</span>
              <div>
                <div className="font-medium">{title}</div>
                <div className="text-muted">{body}</div>
              </div>
            </li>
          ))}
        </ol>
        <div>
          <h3 className="font-medium">How the forecast works</h3>
          <p className="text-muted">{HOW_WE_PICK.forecast}</p>
        </div>
        <div>
          <h3 className="font-medium">What the status words mean</h3>
          <ul className="mt-1 space-y-1">
            {ORDER.map((s, i) => (
              <li key={s} className="flex items-center gap-2 text-muted">
                <StatusGlyph status={s} size={12} />
                <span className="w-24 shrink-0 font-medium text-ink">{STATUS_LABEL[s]}</span>
                <span>{THRESHOLD_LINES[i].split(': ')[1]}</span>
              </li>
            ))}
          </ul>
        </div>
        <div>
          <h3 className="font-medium">Live data</h3>
          <p className="text-muted">{HOW_WE_PICK.freshness}</p>
        </div>
      </div>
    </Panel>
  )
}
