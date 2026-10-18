import type { ReactNode } from 'react'

interface ModalProps {
  title: string
  onClose: () => void
  children: ReactNode
  /** A wider popup for denser content (the driver's tracking). */
  wide?: boolean
}

export function Modal({ title, onClose, children, wide = false }: ModalProps) {
  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className={wide ? 'modal modal--wide' : 'modal'} onClick={(event) => event.stopPropagation()}>
        <div className="modal-header">
          <h3>{title}</h3>
          <button type="button" className="modal-close" onClick={onClose} aria-label="Close">
            ×
          </button>
        </div>
        <div className="modal-body">{children}</div>
      </div>
    </div>
  )
}
