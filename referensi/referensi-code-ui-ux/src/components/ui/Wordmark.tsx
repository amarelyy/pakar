import clsx from 'clsx'
import { APP_NAME } from '@/copy'

export function Wordmark({ className, size = 'md', sub }: { className?: string; size?: 'sm' | 'md'; sub?: string }) {
  const box = size === 'sm' ? 20 : 24
  return (
    <span className={clsx('inline-flex items-center gap-2', className)}>
      <svg width={box} height={box} viewBox="0 0 24 24" aria-hidden="true">
        <rect x="1" y="1" width="22" height="22" rx="6" fill="var(--ink)" />
        <path d="M7 17V7h5.2a3.3 3.3 0 0 1 0 6.6H9.4V17H7zm2.4-5.6h2.6a1.1 1.1 0 0 0 0-2.2H9.4v2.2z" fill="#fff" />
        <circle cx="18" cy="6.5" r="2.4" fill="var(--ok)" stroke="var(--ink)" strokeWidth="1.2" />
      </svg>
      <span className={clsx('font-semibold tracking-tight text-ink', size === 'sm' ? 'text-sm' : 'text-base')}>{APP_NAME}</span>
      {sub && <span className="text-xs text-muted">{sub}</span>}
    </span>
  )
}
