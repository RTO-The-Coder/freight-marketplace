import 'leaflet/dist/leaflet.css'
import L from 'leaflet'

export const OSM_TILE_URL = 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png'
export const OSM_ATTRIBUTION =
  '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'

export const STOP_KIND_COLOR: Record<string, string> = {
  Pickup: '#2563eb',
  Delivery: '#16a34a',
  Office: '#64748b',
}

/** A small round pin, dimmed when the stop is already reached. */
export function dotIcon(color: string, dimmed = false): L.DivIcon {
  return L.divIcon({
    className: '',
    iconSize: [18, 18],
    iconAnchor: [9, 9],
    html: `<span class="tripmap-pin" style="background:${color};opacity:${dimmed ? 0.4 : 1};"></span>`,
  })
}

/** The moving-truck marker. */
export function truckIcon(label?: string): L.DivIcon {
  return L.divIcon({
    className: '',
    iconSize: [26, 26],
    iconAnchor: [13, 13],
    html: `<span class="tripmap-truck" title="${label ?? ''}">🚚</span>`,
  })
}

/** Company-office marker — a square, distinct from round stop pins. */
export function officeIcon(): L.DivIcon {
  return L.divIcon({
    className: '',
    iconSize: [16, 16],
    iconAnchor: [8, 8],
    html: `<span class="tripmap-office"></span>`,
  })
}
