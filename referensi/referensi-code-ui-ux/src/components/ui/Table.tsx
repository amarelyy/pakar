import clsx from 'clsx'
import type { HTMLAttributes, TdHTMLAttributes, ThHTMLAttributes } from 'react'

export function Table({ className, ...rest }: HTMLAttributes<HTMLTableElement>) {
  return (
    <div className="w-full overflow-x-auto">
      <table className={clsx('w-full border-collapse text-sm', className)} {...rest} />
    </div>
  )
}

export function Th({ className, numeric, ...rest }: ThHTMLAttributes<HTMLTableCellElement> & { numeric?: boolean }) {
  return (
    <th
      scope="col"
      className={clsx('h-10 border-b border-hairline px-3 text-xs font-medium text-muted', numeric ? 'text-right' : 'text-left', className)}
      {...rest}
    />
  )
}

export function Td({ className, numeric, ...rest }: TdHTMLAttributes<HTMLTableCellElement> & { numeric?: boolean }) {
  return (
    <td
      className={clsx('h-11 border-b border-hairline px-3 align-middle', numeric ? 'tnum whitespace-nowrap text-right' : 'text-left', className)}
      {...rest}
    />
  )
}
