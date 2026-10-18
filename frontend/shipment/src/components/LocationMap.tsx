import 'leaflet/dist/leaflet.css'
import L from 'leaflet'
import { useEffect } from 'react'
import { MapContainer, Marker, TileLayer, useMap } from 'react-leaflet'
import { OSM_ATTRIBUTION, OSM_TILE_URL } from '@freight/web-ui'

// The default Leaflet marker icon references image URLs that Vite doesn't
// resolve automatically from the leaflet package - rebuild it from the CDN
// so pins actually render instead of showing broken images.
const markerIcon = new L.Icon({
  iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
  iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
  shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
  iconSize: [25, 41],
  iconAnchor: [12, 41],
  popupAnchor: [1, -34],
  shadowSize: [41, 41],
})

const defaultCenter: [number, number] = [51.1657, 10.4515] // Germany, roughly centered
const pointZoom = 11

export interface LatLng {
  latitude: number
  longitude: number
}

interface LocationMapProps {
  value: LatLng | null
  heightPx?: number
}

// MapContainer only reads `center` on mount - move the view when the chosen point changes.
function Recenter({ value }: { value: LatLng | null }) {
  const map = useMap()
  useEffect(() => {
    if (value) map.setView([value.latitude, value.longitude], pointZoom)
  }, [map, value])
  return null
}

/** Shows the chosen place (a saved address or a found area). Display only - places are chosen by address, not by clicking. */
export function LocationMap({ value, heightPx = 200 }: LocationMapProps) {
  const center: [number, number] = value ? [value.latitude, value.longitude] : defaultCenter

  return (
    <div className="map-picker" style={{ height: heightPx }}>
      <MapContainer center={center} zoom={value ? pointZoom : 5} style={{ height: '100%', width: '100%' }}>
        <TileLayer attribution={OSM_ATTRIBUTION} url={OSM_TILE_URL} />
        <Recenter value={value} />
        {value && <Marker position={[value.latitude, value.longitude]} icon={markerIcon} />}
      </MapContainer>
    </div>
  )
}
