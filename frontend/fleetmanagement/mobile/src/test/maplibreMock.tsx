import type { ReactNode } from 'react'
import { Pressable, View, type ViewProps } from 'react-native'

/**
 * Jest stand-in for @maplibre/maplibre-react-native (native code can't run in Jest), wired via
 * moduleNameMapper. Each part renders a plain view carrying its props, so tests can check what
 * the map would draw: line/stop data on the sources, the camera bounds, markers by testID.
 */
type Props = { children?: ReactNode; [key: string]: unknown }

export function Map({ children, ...props }: Props) {
  return (
    <View testID="maplibre-map" {...(props as ViewProps)}>
      {children}
    </View>
  )
}

export function Camera(props: Props) {
  return <View testID="maplibre-camera" {...(props as ViewProps)} />
}

export function GeoJSONSource({ id, children, ...props }: Props) {
  return (
    <View testID={`maplibre-source-${String(id)}`} {...(props as ViewProps)}>
      {children}
    </View>
  )
}

export function Layer() {
  return null
}

export function Marker({ id, onPress, children }: Props) {
  return (
    <Pressable
      testID={`maplibre-marker-${String(id)}`}
      onPress={typeof onPress === 'function' ? () => onPress({ nativeEvent: { id } }) : undefined}
    >
      {children}
    </Pressable>
  )
}
