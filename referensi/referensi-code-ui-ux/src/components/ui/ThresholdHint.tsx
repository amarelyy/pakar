import { THRESHOLD_LINES } from '@/copy'
import { Hint } from './Hint'

export function ThresholdHint() {
  return (
    <Hint label="How status is decided">
      <span className="mb-1 block font-medium">How status is decided</span>
      <ul className="space-y-0.5">
        {THRESHOLD_LINES.map((l) => (
          <li key={l}>{l}</li>
        ))}
      </ul>
    </Hint>
  )
}
