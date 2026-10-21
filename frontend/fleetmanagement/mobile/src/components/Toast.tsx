import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { Pressable, StyleSheet, View } from 'react-native'
import { Text, useTheme } from 'react-native-paper'
import { useAppColors } from '../theme'

/** How long a confirmation stays up, as on the web ("✓ Truck added" closes after 3 s). */
const VISIBLE_MS = 3000

interface ToastMessage {
  title: string
  detail?: string | null
}

const ToastContext = createContext<((message: ToastMessage) => void) | null>(null)

/** Shows the centred "✓ …" confirmation after an action: `useToast()({ title: 'Truck added', detail })`. */
export function useToast(): (message: ToastMessage) => void {
  const show = useContext(ToastContext)
  if (!show) throw new Error('useToast must be used inside ToastProvider')
  return show
}

/** Holds the one confirmation on screen (a newer one replaces it) and draws it over the app. */
export function ToastProvider({ children }: { children: ReactNode }) {
  const [message, setMessage] = useState<(ToastMessage & { key: number }) | null>(null)
  const show = useCallback((m: ToastMessage) => setMessage({ ...m, key: Date.now() }), [])

  // Closes by itself; a new message restarts the time.
  useEffect(() => {
    if (!message) return
    const timer = setTimeout(() => setMessage(null), VISIBLE_MS)
    return () => clearTimeout(timer)
  }, [message])

  const value = useMemo(() => show, [show])
  return (
    <ToastContext.Provider value={value}>
      {children}
      {message && (
        <View style={styles.layer} pointerEvents="box-none">
          <Toast title={message.title} detail={message.detail} onPress={() => setMessage(null)} />
        </View>
      )}
    </ToastContext.Provider>
  )
}

/** The confirmation itself: a green tick, the title and a muted detail line. A tap closes it. */
function Toast({ title, detail, onPress }: ToastMessage & { onPress: () => void }) {
  const theme = useTheme()
  const colors = useAppColors()
  return (
    <Pressable
      onPress={onPress}
      accessibilityRole="alert"
      accessibilityLiveRegion="polite"
      style={[
        styles.toast,
        { backgroundColor: theme.colors.surfaceVariant, borderColor: colors.borderStrong },
      ]}
    >
      <View style={[styles.tick, { backgroundColor: colors.officeBg }]}>
        <Text style={[styles.tickText, { color: colors.office }]}>✓</Text>
      </View>
      <View style={styles.text}>
        <Text variant="titleSmall">{title}</Text>
        {detail ? (
          <Text variant="bodySmall" style={{ color: theme.colors.onSurfaceVariant }}>
            {detail}
          </Text>
        ) : null}
      </View>
    </Pressable>
  )
}

const styles = StyleSheet.create({
  layer: { ...StyleSheet.absoluteFill, alignItems: 'center', justifyContent: 'center' },
  toast: {
    width: '80%',
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    paddingHorizontal: 16,
    paddingVertical: 14,
    borderRadius: 16,
    borderWidth: 1,
    elevation: 8,
  },
  tick: { width: 30, height: 30, borderRadius: 15, alignItems: 'center', justifyContent: 'center' },
  tickText: { fontWeight: '800' },
  text: { flex: 1 },
})
