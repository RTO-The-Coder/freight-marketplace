import { PopupFrame, type PopupContentProps } from './popupParts'

/**
 * A full-height panel from the right for bigger work (Assign a shipment, Driver details,
 * Offers), the page still visible beside it under a lighter dimming. Same header, body and
 * footer as the centred {@link Modal}; Esc, a click outside or × closes it. Not the page
 * column {@link SidePanel}.
 */
export function OverlayPanel(props: PopupContentProps) {
  return <PopupFrame {...props} as="aside" scrimClassName="popup-scrim popup-scrim--panel" className="popup overlay-panel" />
}
