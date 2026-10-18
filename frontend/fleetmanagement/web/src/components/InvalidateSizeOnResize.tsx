import { useEffect } from 'react'
import { useMap } from 'react-leaflet'

/**
 * Tells Leaflet to re-measure when its container changes size (e.g. a side list on the
 * company page is hidden or shown) — otherwise tiles stay drawn for the old size.
 */
export function InvalidateSizeOnResize() {
  const map = useMap()
  useEffect(() => {
    const observer = new ResizeObserver(() => map.invalidateSize())
    observer.observe(map.getContainer())
    return () => observer.disconnect()
  }, [map])
  return null
}
