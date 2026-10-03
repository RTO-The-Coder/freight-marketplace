import { useState } from 'react'
import { Modal, Pressable, StyleSheet, View } from 'react-native'
import { SafeAreaView } from 'react-native-safe-area-context'
import { Appbar, Icon, Text, useTheme } from 'react-native-paper'
import { OSM_ATTRIBUTION } from './osmStyle'
import { RouteMap } from './RouteMap'
import type { MapScene } from './scene'

interface Props {
  /** Title of the full-screen map. */
  title: string
  scene: MapScene
  /** Loading / straight-line note shown under the map, if any. */
  status: string | null
  /** Called from the full-screen map; the map closes first. */
  onPressTruck?: (truckId: string) => void
  height?: number
}

/**
 * A static map preview inside a scrolling screen (no gestures, so the page scrolls
 * over it). Tapping it opens the same map full screen, with pan and zoom.
 */
export function MapPreview({ title, scene, status, onPressTruck, height = 220 }: Props) {
  const theme = useTheme()
  const [open, setOpen] = useState(false)
  const [selected, setSelected] = useState<string | null>(null)

  const close = () => {
    setOpen(false)
    setSelected(null)
  }

  const statusText = status ? (
    <Text variant="bodySmall" style={[styles.status, { color: theme.colors.onSurfaceVariant }]}>
      {status}
    </Text>
  ) : null

  return (
    <View>
      <View style={[styles.preview, { height, borderColor: theme.colors.outlineVariant }]}>
        <RouteMap scene={scene} interactive={false} style={StyleSheet.absoluteFill} />
        {/* Catches every touch: a tap opens full screen, a drag scrolls the page. */}
        <Pressable
          style={StyleSheet.absoluteFill}
          onPress={() => setOpen(true)}
          accessibilityRole="button"
          accessibilityLabel={`Open ${title.toLowerCase()} full screen`}
        >
          <View style={[styles.expand, { backgroundColor: theme.colors.surface }]}>
            <Icon source="arrow-expand" size={18} color={theme.colors.onSurface} />
          </View>
          <Text style={styles.attribution}>{OSM_ATTRIBUTION}</Text>
        </Pressable>
      </View>
      {statusText}

      <Modal visible={open} animationType="slide" onRequestClose={close}>
        <SafeAreaView style={[styles.screen, { backgroundColor: theme.colors.background }]}>
          <Appbar.Header>
            <Appbar.BackAction onPress={close} accessibilityLabel="Close map" />
            <Appbar.Content title={title} />
          </Appbar.Header>
          <RouteMap
            scene={scene}
            interactive
            style={styles.screen}
            onPressStop={setSelected}
            onPressTruck={
              onPressTruck
                ? (id) => {
                    close()
                    onPressTruck(id)
                  }
                : undefined
            }
          />
          {(selected || status) && (
            <View style={[styles.info, { backgroundColor: theme.colors.surface }]}>
              {selected && <Text variant="bodyLarge">{selected}</Text>}
              {statusText}
            </View>
          )}
        </SafeAreaView>
      </Modal>
    </View>
  )
}

const styles = StyleSheet.create({
  screen: { flex: 1 },
  preview: { overflow: 'hidden', borderRadius: 12, borderWidth: StyleSheet.hairlineWidth },
  expand: { position: 'absolute', top: 8, right: 8, borderRadius: 16, padding: 6 },
  attribution: {
    position: 'absolute',
    right: 4,
    bottom: 2,
    fontSize: 10,
    color: '#334155',
    backgroundColor: 'rgba(255,255,255,0.7)',
    paddingHorizontal: 4,
  },
  status: { paddingTop: 4 },
  info: { padding: 16, gap: 4 },
})
