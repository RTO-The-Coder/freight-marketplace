// Shared web UI for the fleet web app and the shipper web app: the design tokens and component
// styles (import './styles/*.css' from an app's entry), and the components both apps use.
// Web only - the Android app shares fleetmanagement-core, never this package.
export { FitBounds } from './components/FitBounds'
export { InvalidateSizeOnResize } from './components/InvalidateSizeOnResize'
export { Logo } from './components/Logo'
export { OSM_ATTRIBUTION, OSM_TILE_URL, dotIcon, fleetTruckIcon, officeIcon, truckIcon } from './components/mapPrimitives'
export { DateTimeFields } from './components/DateTimeFields'
export { Modal } from './components/Modal'
export { OverlayPanel } from './components/OverlayPanel'
export { SidePanel } from './components/SidePanel'
export { SimClockBar } from './components/SimClockBar'
export { Toast } from './components/Toast'
