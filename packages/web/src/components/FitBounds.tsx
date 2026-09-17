import L from 'leaflet'
import { useEffect } from 'react'
import { useMap } from 'react-leaflet'
import type { LatLng } from './useRouteGeometry'

/** Fits the map to every point drawn, re-running when the point set changes. */
export function FitBounds({ points }: { points: LatLng[] }) {
  const map = useMap()
  const signature = points.map((p) => `${p[0].toFixed(3)},${p[1].toFixed(3)}`).join('|')
  useEffect(() => {
    if (points.length === 0) return
    if (points.length === 1) {
      map.setView(points[0], 10)
      return
    }
    map.fitBounds(L.latLngBounds(points), { padding: [32, 32], maxZoom: 12 })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [map, signature])
  return null
}
