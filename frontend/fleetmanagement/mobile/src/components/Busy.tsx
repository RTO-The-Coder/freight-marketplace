import { onRequestsInFlightChange } from '@freight/api-client'
import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'
import { AccessibilityInfo, Animated, Easing, StyleSheet, View, type LayoutChangeEvent } from 'react-native'
import { useTheme } from 'react-native-paper'

/** The bar waits this long before showing, so quick calls don't flicker. */
const SHOW_AFTER_MS = 300

const BusyContext = createContext(false)

/**
 * Follows every request in flight (all calls go through @freight/api-client) and says "busy"
 * once something has been loading for 0.3 s, until nothing is.
 */
export function BusyProvider({ children }: { children: ReactNode }) {
  const [shown, setShown] = useState(false)

  useEffect(() => {
    // Started when the first request goes out; a request finishing while others still run changes nothing.
    let timer: ReturnType<typeof setTimeout> | null = null
    const unsubscribe = onRequestsInFlightChange((count) => {
      if (count === 0) {
        if (timer) clearTimeout(timer)
        timer = null
        setShown(false)
      } else if (!timer) {
        timer = setTimeout(() => setShown(true), SHOW_AFTER_MS)
      }
    })
    return () => {
      unsubscribe()
      if (timer) clearTimeout(timer)
    }
  }, [])

  return <BusyContext.Provider value={shown}>{children}</BusyContext.Provider>
}

/** Whether the app has been waiting on a request for more than 0.3 s. */
export function useIsBusy(): boolean {
  return useContext(BusyContext)
}

/** A stack's `screenLayout`: every screen with the busy bar along its top edge, just under the app bar. */
export function withBusyBar({ children }: { children: ReactNode }) {
  return (
    <View style={styles.screen}>
      {children}
      <BusyBar />
    </View>
  )
}

/** The thin accent bar under the app bar, sliding while anything loads; takes no room when idle. */
export function BusyBar() {
  const theme = useTheme()
  const busy = useIsBusy()
  const [width, setWidth] = useState(0)
  const [reduceMotion, setReduceMotion] = useState(false)
  const [slide] = useState(() => new Animated.Value(0))

  useEffect(() => {
    void AccessibilityInfo.isReduceMotionEnabled().then(setReduceMotion)
  }, [])

  useEffect(() => {
    if (!busy || width === 0) return
    slide.setValue(0)
    const loop = Animated.loop(
      Animated.timing(slide, {
        toValue: 1,
        duration: reduceMotion ? 4000 : 1100,
        easing: Easing.inOut(Easing.ease),
        useNativeDriver: true,
      }),
    )
    loop.start()
    return () => loop.stop()
  }, [busy, width, reduceMotion, slide])

  const segment = width * 0.35
  const translateX = slide.interpolate({ inputRange: [0, 1], outputRange: [-segment, width] })

  return (
    <View
      style={[styles.track, { opacity: busy ? 1 : 0 }]}
      onLayout={(e: LayoutChangeEvent) => setWidth(e.nativeEvent.layout.width)}
      pointerEvents="none"
      accessibilityElementsHidden={!busy}
      accessibilityLabel={busy ? 'Loading' : undefined}
      testID="busy-bar"
    >
      <Animated.View
        style={[styles.segment, { width: segment, backgroundColor: theme.colors.primary, transform: [{ translateX }] }]}
      />
    </View>
  )
}

const styles = StyleSheet.create({
  screen: { flex: 1 },
  // Overlays the top edge of the screen below the app bar, so showing it never moves the content.
  track: { position: 'absolute', top: 0, left: 0, right: 0, height: 3, overflow: 'hidden', zIndex: 10 },
  segment: { position: 'absolute', top: 0, bottom: 0, borderRadius: 2 },
})
