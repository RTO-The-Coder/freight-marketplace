import { MaterialCommunityIcons } from '@expo/vector-icons'
import {
  Camera,
  GeoJSONSource,
  Layer,
  Map as MapView,
  Marker,
  type LineLayerSpecification,
} from '@maplibre/maplibre-react-native'
import { useMemo } from 'react'
import { StyleSheet, View, type StyleProp, type ViewStyle } from 'react-native'
import { OSM_STYLE } from './osmStyle'
import { boundsOf, type MapScene } from './scene'

interface Props {
  scene: MapScene
  /** Pan and zoom (full screen). A preview is static so the page around it scrolls freely. */
  interactive: boolean
  onPressTruck?: (truckId: string) => void
  onPressStop?: (label: string) => void
  style?: StyleProp<ViewStyle>
}

const CAMERA_PADDING = { top: 40, right: 40, bottom: 40, left: 40 }

/** Draws a MapScene on OpenStreetMap: route lines, stop dots, office and truck markers. */
export function RouteMap({ scene, interactive, onPressTruck, onPressStop, style }: Props) {
  // Re-fit only when the points actually change, so a pan isn't undone on every re-render.
  const boundsKey = JSON.stringify(boundsOf(scene.fitPoints))
  const bounds = useMemo(() => JSON.parse(boundsKey) as ReturnType<typeof boundsOf>, [boundsKey])

  const lines = useMemo<GeoJSON.FeatureCollection>(
    () => ({
      type: 'FeatureCollection',
      features: scene.lines.map((l) => ({
        type: 'Feature',
        // GeoJSON is [lng, lat]; core paths are [lat, lng].
        geometry: { type: 'LineString', coordinates: l.path.map(([lat, lng]) => [lng, lat]) },
        properties: { color: l.color, width: l.width, opacity: l.opacity, dashed: l.dashed },
      })),
    }),
    [scene.lines],
  )

  const dots = useMemo<GeoJSON.FeatureCollection>(
    () => ({
      type: 'FeatureCollection',
      features: scene.dots.map((d) => ({
        type: 'Feature',
        geometry: { type: 'Point', coordinates: [d.longitude, d.latitude] },
        properties: { id: d.id, color: d.color, opacity: d.dimmed ? 0.4 : 1, label: d.label },
      })),
    }),
    [scene.dots],
  )

  const linePaint: LineLayerSpecification['paint'] = {
    'line-color': ['get', 'color'],
    'line-width': ['get', 'width'],
    'line-opacity': ['get', 'opacity'],
  }

  return (
    <MapView
      style={style}
      mapStyle={OSM_STYLE}
      dragPan={interactive}
      touchZoom={interactive}
      doubleTapZoom={interactive}
      doubleTapHoldZoom={interactive}
      touchRotate={false}
      touchPitch={false}
      compass={false}
      logo={false}
      attribution={interactive}
    >
      {bounds && <Camera bounds={bounds} padding={CAMERA_PADDING} duration={0} />}

      <GeoJSONSource id="route-lines" data={lines}>
        <Layer
          id="route-lines-road"
          type="line"
          filter={['==', ['get', 'dashed'], false]}
          layout={{ 'line-cap': 'round', 'line-join': 'round' }}
          paint={linePaint}
        />
        <Layer
          id="route-lines-straight"
          type="line"
          filter={['==', ['get', 'dashed'], true]}
          paint={{ ...linePaint, 'line-dasharray': [2, 2] }}
        />
      </GeoJSONSource>

      <GeoJSONSource
        id="route-stops"
        data={dots}
        onPress={(e) => {
          const label = e.nativeEvent.features[0]?.properties?.label
          if (typeof label === 'string') onPressStop?.(label)
        }}
      >
        <Layer
          id="route-stops-circles"
          type="circle"
          paint={{
            'circle-radius': 7,
            'circle-color': ['get', 'color'],
            'circle-opacity': ['get', 'opacity'],
            'circle-stroke-color': '#ffffff',
            'circle-stroke-width': 2,
            'circle-stroke-opacity': ['get', 'opacity'],
          }}
        />
      </GeoJSONSource>

      {scene.office && (
        <Marker id="office" lngLat={[scene.office.longitude, scene.office.latitude]}>
          <View style={styles.office} accessibilityLabel="Company office" />
        </Marker>
      )}

      {scene.trucks.map((t) => (
        <Marker
          key={t.id}
          id={`truck-${t.id}`}
          lngLat={[t.longitude, t.latitude]}
          onPress={onPressTruck ? () => onPressTruck(t.id) : undefined}
        >
          <View style={styles.truck} accessibilityLabel={t.label}>
            <MaterialCommunityIcons name="truck" size={16} color="#ffffff" />
          </View>
        </Marker>
      ))}
    </MapView>
  )
}

const styles = StyleSheet.create({
  // Square office, distinct from the round stop dots (as on web).
  office: { width: 16, height: 16, backgroundColor: '#475569', borderColor: '#ffffff', borderWidth: 2 },
  truck: {
    width: 28,
    height: 28,
    borderRadius: 14,
    backgroundColor: '#0f172a',
    borderColor: '#ffffff',
    borderWidth: 2,
    alignItems: 'center',
    justifyContent: 'center',
  },
})
