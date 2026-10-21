import { useEffect, useRef, useState, type ReactNode } from 'react'
import { Animated, StyleSheet, View, type StyleProp, type ViewStyle } from 'react-native'
import { ActivityIndicator, Text, useTheme } from 'react-native-paper'
import { useAppColors } from '../theme'

/** The small "Updating" tag next to a section title while its content reloads. */
export function UpdatingTag({ visible }: { visible: boolean }) {
  const theme = useTheme()
  const colors = useAppColors()
  if (!visible) return null
  return (
    <View style={[styles.tag, { backgroundColor: colors.accentBg }]} accessibilityLabel="Updating" testID="updating-tag">
      <ActivityIndicator size={10} color={theme.colors.primary} />
      <Text variant="labelSmall" style={[styles.tagText, { color: theme.colors.primary }]}>
        Updating
      </Text>
    </View>
  )
}

/** While `stale`, the content stays where it is, faded a little, until the new data replaces it. */
export function Stale({ stale, children, style }: { stale: boolean; children: ReactNode; style?: StyleProp<ViewStyle> }) {
  const [opacity] = useState(() => new Animated.Value(1))
  useEffect(() => {
    Animated.timing(opacity, { toValue: stale ? 0.55 : 1, duration: 200, useNativeDriver: true }).start()
  }, [stale, opacity])
  return <Animated.View style={[style, { opacity }]}>{children}</Animated.View>
}

/**
 * Lights its content up briefly in the accent tint when `signature` changes (a card whose data
 * changed after a reload); never on the first show.
 */
export function Flash({ signature, children, style }: { signature: string; children: ReactNode; style?: StyleProp<ViewStyle> }) {
  const colors = useAppColors()
  const [glow] = useState(() => new Animated.Value(0))
  const previous = useRef(signature)

  useEffect(() => {
    if (previous.current === signature) return
    previous.current = signature
    glow.setValue(1)
    Animated.timing(glow, { toValue: 0, duration: 1200, useNativeDriver: true }).start()
  }, [signature, glow])

  return (
    <View style={style}>
      {children}
      <Animated.View
        pointerEvents="none"
        style={[StyleSheet.absoluteFill, styles.glow, { backgroundColor: colors.accentBg, opacity: glow }]}
      />
    </View>
  )
}

const styles = StyleSheet.create({
  tag: { flexDirection: 'row', alignItems: 'center', gap: 6, borderRadius: 999, paddingHorizontal: 8, paddingVertical: 1 },
  tagText: { fontWeight: '600' },
  glow: { borderRadius: 14 },
})
