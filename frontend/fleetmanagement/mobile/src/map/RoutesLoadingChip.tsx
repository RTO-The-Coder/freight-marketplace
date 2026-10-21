import { StyleSheet, View } from 'react-native'
import { ActivityIndicator, Text, useTheme } from 'react-native-paper'
import { useSlow } from '../components/useSlow'

/**
 * "Loading road routes…" on a map until its routes are drawn; after 8 s it says the routing
 * server is slow. `raised` lifts it above a panel at the bottom of the map.
 */
export function RoutesLoadingChip({ raised = false }: { raised?: boolean }) {
  const theme = useTheme()
  const slow = useSlow(true)
  return (
    <View
      pointerEvents="none"
      style={[styles.chip, raised && styles.raised, { backgroundColor: theme.colors.surface, borderColor: theme.colors.outline }]}
      accessibilityLiveRegion="polite"
    >
      <ActivityIndicator size={12} color={theme.colors.primary} />
      <Text variant="labelMedium" style={styles.text}>
        {slow ? 'Still loading routes, the routing server is slow…' : 'Loading road routes…'}
      </Text>
    </View>
  )
}

const styles = StyleSheet.create({
  chip: {
    position: 'absolute',
    left: 10,
    bottom: 10,
    maxWidth: '85%',
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    borderRadius: 999,
    borderWidth: StyleSheet.hairlineWidth,
    paddingHorizontal: 10,
    paddingVertical: 4,
  },
  raised: { bottom: 96 },
  text: { flexShrink: 1 },
})
