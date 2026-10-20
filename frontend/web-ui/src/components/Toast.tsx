import { useEffect, useRef, type ReactNode } from 'react'
import { useEscapeToClose } from './popupParts'

interface ToastProps {
  /** done: ✓ · progress: a spinner · error: ⚠ (stays until closed). */
  kind: 'done' | 'progress' | 'error'
  /** center: a confirmation ("Truck added") · top: the sim clock's messages. */
  placement: 'center' | 'top'
  title: ReactNode
  /** The line under the title ("FL-14 · Flatbed · Large"). */
  detail?: ReactNode
  /** Closes by itself after this many ms (a confirmation: ~3000). */
  autoCloseMs?: number
  /**
   * Esc or a click anywhere closes it sooner; an error gets a ×. Left out (a message about work
   * in progress): it stays until the caller removes it.
   */
  onClose?: () => void
  /** false: only its own timer closes it, never Esc or a click (the sim clock's result). */
  dismissible?: boolean
}

const ICONS = { done: '✓', error: '⚠' } as const

/** A message in the same card style as the popups, over everything including the maps. */
export function Toast({ kind, placement, title, detail, autoCloseMs, onClose, dismissible = true }: ToastProps) {
  const close = useRef(onClose)
  close.current = onClose
  useEscapeToClose(dismissible ? onClose : undefined)

  useEffect(() => {
    if (!autoCloseMs || !close.current) return
    const timer = window.setTimeout(() => close.current?.(), autoCloseMs)
    return () => window.clearTimeout(timer)
  }, [autoCloseMs, title, detail])

  // A click anywhere closes a confirmation sooner (never an error - it has its ×).
  const closesOnClick = dismissible && onClose !== undefined && kind !== 'error'
  useEffect(() => {
    if (!closesOnClick) return
    const onPointerDown = () => close.current?.()
    // Added after the click that showed the message has finished.
    const timer = window.setTimeout(() => document.addEventListener('pointerdown', onPointerDown))
    return () => {
      window.clearTimeout(timer)
      document.removeEventListener('pointerdown', onPointerDown)
    }
  }, [closesOnClick])

  return (
    <div className={`toast toast--${placement} toast--${kind}`} role={kind === 'error' ? 'alert' : 'status'}>
      {kind === 'progress' ? (
        <span className="toast__spinner" aria-hidden="true" />
      ) : (
        <span className="toast__icon" aria-hidden="true">
          {ICONS[kind]}
        </span>
      )}
      <span className="toast__text">
        <strong>{title}</strong>
        {detail && <span>{detail}</span>}
      </span>
      {kind === 'error' && onClose && (
        <button type="button" className="toast__close" onClick={onClose} aria-label="Close">
          ×
        </button>
      )}
    </div>
  )
}
