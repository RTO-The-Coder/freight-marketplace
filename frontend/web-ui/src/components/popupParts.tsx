import { useEffect, useId, useRef, type MouseEvent, type ReactNode, type RefObject } from 'react'

// What the centred box (Modal) and the side panel (OverlayPanel) share: the header / body /
// footer frame, Esc, click outside and moving focus in and back out.

/** Open popups and messages that close on Esc, newest last - Esc closes only the newest. */
const escapeStack: Array<{ current: () => void }> = []

function onEscape(event: KeyboardEvent) {
  if (event.key !== 'Escape' || escapeStack.length === 0) return
  event.preventDefault()
  escapeStack[escapeStack.length - 1].current()
}

/** Esc calls `onClose` while this popup is the newest one open. */
export function useEscapeToClose(onClose: (() => void) | undefined) {
  const handler = useRef(onClose ?? (() => {}))
  handler.current = onClose ?? (() => {})
  const enabled = onClose !== undefined

  useEffect(() => {
    if (!enabled) return
    if (escapeStack.length === 0) document.addEventListener('keydown', onEscape)
    escapeStack.push(handler)
    return () => {
      escapeStack.splice(escapeStack.indexOf(handler), 1)
      if (escapeStack.length === 0) document.removeEventListener('keydown', onEscape)
    }
  }, [enabled])
}

/** Focus moves into the popup when it opens and back to where it was when it closes. */
function useFocusInside(ref: RefObject<HTMLElement | null>) {
  useEffect(() => {
    const before = document.activeElement instanceof HTMLElement ? document.activeElement : null
    ref.current?.focus({ preventScroll: true })
    return () => before?.focus({ preventScroll: true })
  }, [ref])
}

/**
 * Closes on a click on the dimmed background only - a press that starts inside the popup
 * (selecting text, dragging) and ends outside doesn't count.
 */
function useScrimClick(onClose: () => void) {
  const pressedOnScrim = useRef(false)
  return {
    onMouseDown: (event: MouseEvent) => {
      pressedOnScrim.current = event.target === event.currentTarget
    },
    onClick: (event: MouseEvent) => {
      if (pressedOnScrim.current && event.target === event.currentTarget) onClose()
      pressedOnScrim.current = false
    },
  }
}

export interface PopupContentProps {
  /** Sentence case: "Add a truck", "Change trip start". */
  title: string
  /** The line under the title: the company, truck or shipment it is about. */
  context?: ReactNode
  onClose: () => void
  /** The body; it scrolls when it is taller than the popup. */
  children: ReactNode
  /**
   * The actions, in a footer that always stays visible: Cancel and the main action named after
   * what it does. A link that leads elsewhere goes first with `className="popup__footer-start"`.
   */
  footer?: ReactNode
  /** A result that must stay in sight (fits / doesn't fit), fixed just above the footer. */
  status?: ReactNode
}

interface PopupFrameProps extends PopupContentProps {
  /** The dimmed background's classes. */
  scrimClassName: string
  /** The popup's own classes. */
  className: string
  /** `aside` for the side panel. */
  as: 'div' | 'aside'
}

/** The dimmed background and the popup on it: header (title, context, ×), body, footer. */
export function PopupFrame({ title, context, onClose, children, footer, status, scrimClassName, className, as }: PopupFrameProps) {
  const ref = useRef<HTMLElement>(null)
  const titleId = useId()
  useEscapeToClose(onClose)
  useFocusInside(ref)
  const scrim = useScrimClick(onClose)
  const Box = as

  return (
    <div className={scrimClassName} {...scrim}>
      <Box
        ref={ref as RefObject<HTMLDivElement & HTMLElement>}
        className={className}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        tabIndex={-1}
      >
        <div className="popup__header">
          <div className="popup__heading">
            <h3 id={titleId} className="popup__title">
              {title}
            </h3>
            {context && <div className="popup__context">{context}</div>}
          </div>
          <button type="button" className="popup__close" onClick={onClose} aria-label="Close">
            ×
          </button>
        </div>
        <div className="popup__body">{children}</div>
        {status && <div className="popup__status">{status}</div>}
        {footer && <div className="popup__footer">{footer}</div>}
      </Box>
    </div>
  )
}
