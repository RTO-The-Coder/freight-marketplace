import 'leaflet/dist/leaflet.css'
import L from 'leaflet'
import { MapContainer, Marker, TileLayer, useMapEvents } from 'react-leaflet'

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

export interface LatLng {
  latitude: number
  longitude: number
}

interface LocationMapPickerProps {
  value: LatLng | null
  onChange: (location: LatLng) => void
  heightPx?: number
}

function ClickHandler({ onChange }: { onChange: (location: LatLng) => void }) {
  useMapEvents({
    click: (event) => onChange({ latitude: event.latlng.lat, longitude: event.latlng.lng }),
  })
  return null
}

export function LocationMapPicker({ value, onChange, heightPx = 240 }: LocationMapPickerProps) {
  const center: [number, number] = value ? [value.latitude, value.longitude] : defaultCenter

  return (
    <div className="map-picker" style={{ height: heightPx }}>
      <MapContainer center={center} zoom={value ? 11 : 6} style={{ height: '100%', width: '100%' }}>
        <TileLayer
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        />
        <ClickHandler onChange={onChange} />
        {value && <Marker position={[value.latitude, value.longitude]} icon={markerIcon} />}
      </MapContainer>
    </div>
  )
}
