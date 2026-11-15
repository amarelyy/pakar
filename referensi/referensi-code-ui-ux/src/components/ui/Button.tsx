import clsx from 'clsx'
import type { ButtonHTMLAttributes } from 'react'

type Variant = 'primary' | 'secondary' | 'ghost' | 'danger'
type Size = 'sm' | 'md' | 'lg'

const V: Record<Variant, string> = {
  primary: 'bg-accent text-white hover:bg-accent-ink',
  secondary: 'bg-card text-ink border border-hairline hover:bg-ground',
  ghost: 'bg-transparent text-ink hover:bg-ground',
  danger: 'bg-bad text-white hover:opacity-90',
}
const S: Record<Size, string> = {
  sm: 'h-8 px-2.5 text-xs',
  md: 'h-10 px-3.5 text-sm',
  lg: 'h-12 px-4 text-base',
}

export function Button({ variant = 'secondary', size = 'md', className, ...rest }: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: Variant; size?: Size }) {
  return (
    <button
      type="button"
      className={clsx(
        'inline-flex items-center justify-center gap-2 rounded-card font-medium transition-colors disabled:cursor-not-allowed disabled:opacity-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-2',
        V[variant],
        S[size],
        className,
      )}
      {...rest}
    />
  )
}
