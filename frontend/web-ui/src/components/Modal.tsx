import { PopupFrame, type PopupContentProps } from './popupParts'

interface ModalProps extends PopupContentProps {
  /** small ~440 px (default) or medium ~580 px. */
  size?: 'small' | 'medium'
  /** @deprecated Use `size="medium"`; kept until every popup has moved. */
  wide?: boolean
}

/**
 * A centred box for small forms (Add truck, Change trip start, ...) over the dimmed page.
 * Esc, a click outside or × closes it. Bigger work opens in an {@link OverlayPanel}.
 */
export function Modal({ size, wide = false, ...content }: ModalProps) {
  const width = size ?? (wide ? 'medium' : 'small')
  return <PopupFrame {...content} as="div" scrimClassName="popup-scrim" className={`popup modal modal--${width}`} />
}
