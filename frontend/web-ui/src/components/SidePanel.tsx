import type { ReactNode } from 'react'

interface SidePanelProps {
  title: string
  /** Shown after the title ("Fleet (4)"); null while loading. */
  count: number | null
  children: ReactNode
}

/**
 * A column of a three-column page (Fleet, Drivers, Shipments, Saved addresses, ...): a heading
 * with its count, and a body that scrolls on its own.
 */
export function SidePanel({ title, count, children }: SidePanelProps) {
  return (
    <section className="side-panel" aria-label={title}>
      <div className="side-panel__head">
        <h3>{count === null ? title : `${title} (${count})`}</h3>
      </div>
      <div className="side-panel__body">{children}</div>
    </section>
  )
}
