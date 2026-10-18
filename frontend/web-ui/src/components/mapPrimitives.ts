import 'leaflet/dist/leaflet.css'
import L from 'leaflet'

export const OSM_TILE_URL = 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png'
export const OSM_ATTRIBUTION =
  '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'

/** A small round pin, dimmed when the stop is already reached (or another one is selected), larger when highlighted. */
export function dotIcon(color: string, dimmed = false, highlighted = false): L.DivIcon {
  const size = highlighted ? 26 : 18
  return L.divIcon({
    className: '',
    iconSize: [size, size],
    iconAnchor: [size / 2, size / 2],
    html: `<span class="tripmap-pin${highlighted ? ' tripmap-pin--highlighted' : ''}" style="background:${color};opacity:${dimmed ? 0.4 : 1};"></span>`,
  })
}

/** The moving-truck marker, dimmed when another one is selected, larger when highlighted. */
export function truckIcon(label?: string, dimmed = false, highlighted = false): L.DivIcon {
  const size = highlighted ? 36 : 26
  return L.divIcon({
    className: '',
    iconSize: [size, size],
    iconAnchor: [size / 2, size / 2],
    html: `<span class="tripmap-truck${highlighted ? ' tripmap-truck--highlighted' : ''}" style="opacity:${dimmed ? 0.4 : 1};" title="${label ?? ''}">🚚</span>`,
  })
}

/**
 * A fleet-map truck: the truck in a ring of its route colour, larger when highlighted.
 * `offset` shifts it by that many pixels from its position — used to fan out trucks
 * parked at the office so they don't sit on top of each other.
 */
export function fleetTruckIcon(color: string, highlighted = false, offset: [number, number] = [0, 0]): L.DivIcon {
  const size = highlighted ? 36 : 28
  return L.divIcon({
    className: '',
    iconSize: [size, size],
    iconAnchor: [size / 2 - offset[0], size / 2 - offset[1]],
    html: `<span class="fleetmap-truck${highlighted ? ' fleetmap-truck--highlighted' : ''}" style="border-color:${color};">🚚</span>`,
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
