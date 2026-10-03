import type { StyleSpecification } from '@maplibre/maplibre-react-native'

/** OpenStreetMap raster tiles — the same tiles as the web maps; free, no API key. */
export const OSM_STYLE: StyleSpecification = {
  version: 8,
  sources: {
    osm: {
      type: 'raster',
      tiles: ['https://tile.openstreetmap.org/{z}/{x}/{y}.png'],
      tileSize: 256,
      maxzoom: 19,
      attribution: '© OpenStreetMap contributors',
    },
  },
  layers: [{ id: 'osm', type: 'raster', source: 'osm' }],
}

export const OSM_ATTRIBUTION = '© OpenStreetMap'
