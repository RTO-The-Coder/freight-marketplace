import { useEffect, useState } from 'react'
import { AccessibilityInfo, Animated, Easing, StyleSheet, View, type DimensionValue, type LayoutChangeEvent } from 'react-native'
import { useTheme } from 'react-native-paper'

/** The shine's soft edges without a gradient library: thin slices, faint at the sides. */
const SHINE_SLICES = [0.15, 0.35, 0.6, 0.8, 0.6, 0.35, 0.15]

/** One grey placeholder block with a light shine sweeping across, while the screen opens. */
export function Skeleton({ height, width = '100%', radius = 8 }: { height: number; width?: DimensionValue; radius?: number }) {
  const theme = useTheme()
  const [blockWidth, setBlockWidth] = useState(0)
  const [reduceMotion, setReduceMotion] = useState(false)
  const [sweep] = useState(() => new Animated.Value(0))

  useEffect(() => {
    void AccessibilityInfo.isReduceMotionEnabled().then(setReduceMotion)
  }, [])

  useEffect(() => {
    if (blockWidth === 0) return
    sweep.setValue(0)
    const loop = Animated.loop(
      Animated.timing(sweep, {
        toValue: 1,
        duration: reduceMotion ? 4000 : 1400,
        easing: Easing.linear,
        useNativeDriver: true,
      }),
    )
    loop.start()
    return () => loop.stop()
  }, [blockWidth, reduceMotion, sweep])

  const shine = blockWidth * 0.4
  const translateX = sweep.interpolate({ inputRange: [0, 1], outputRange: [-shine, blockWidth] })
  // The web's skeleton: sunken grey with a lighter band (white on light, the border grey on dark).
  const shineColor = theme.dark ? theme.colors.outline : theme.colors.surface

  return (
    <View
      style={[styles.block, { height, width, borderRadius: radius, backgroundColor: theme.colors.surfaceVariant }]}
      onLayout={(e: LayoutChangeEvent) => setBlockWidth(e.nativeEvent.layout.width)}
    >
      <Animated.View style={[styles.shine, { width: shine, transform: [{ translateX }] }]}>
        {SHINE_SLICES.map((opacity, i) => (
          <View key={i} style={[styles.slice, { backgroundColor: shineColor, opacity }]} />
        ))}
      </Animated.View>
    </View>
  )
}

/** A screen opening: a short bar (the switch or tabs) and a few card-sized blocks. */
export function SkeletonList({ cards = 4, cardHeight = 96 }: { cards?: number; cardHeight?: number }) {
  return (
    <View style={styles.list} accessibilityLabel="Loading" testID="skeleton-list">
      <Skeleton height={38} />
      {Array.from({ length: cards }, (_, i) => (
        <Skeleton key={i} height={cardHeight} radius={14} />
      ))}
    </View>
  )
}

const styles = StyleSheet.create({
  block: { overflow: 'hidden' },
  shine: { position: 'absolute', top: 0, bottom: 0, flexDirection: 'row' },
  slice: { flex: 1 },
  list: { padding: 12, gap: 10 },
})
