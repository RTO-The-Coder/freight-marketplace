import { useRef, useState } from 'react'
import { StyleSheet, View, type StyleProp, type ViewStyle } from 'react-native'
import { Button, Text, useTheme, type ButtonProps } from 'react-native-paper'
import { useSlow } from './useSlow'

export const SLOW_ACTION_TEXT = 'Still working, the routing server is slow…'

interface Props extends Omit<ButtonProps, 'onPress' | 'loading' | 'children' | 'style'> {
  /** "Assign to Flatbed 1". */
  label: string
  /** Shown with a spinner while the action runs: "Assigning…". */
  busyLabel: string
  /** The action; while its promise runs the button is busy and can't be pressed again. */
  onPress: () => Promise<unknown> | void
  /** Busy from outside too (an action the screen runs itself). */
  busy?: boolean
  /** Placed on the wrapper, so `flex: 1` in a sheet's footer works. */
  style?: StyleProp<ViewStyle>
}

/**
 * A button for an action that waits on the server: a small spinner and its "…ing" text while
 * it runs, no second press, and after 8 s a note that the routing server is slow.
 */
export function ActionButton({ label, busyLabel, onPress, busy = false, disabled, style, ...button }: Props) {
  const theme = useTheme()
  const [running, setRunning] = useState(false)
  // Set at once on press: a quick second tap arrives before the re-render that disables the button.
  const pressed = useRef(false)
  const working = running || busy
  const slow = useSlow(working)

  const press = async () => {
    if (pressed.current || working) return
    pressed.current = true
    setRunning(true)
    try {
      await onPress()
    } finally {
      pressed.current = false
      setRunning(false)
    }
  }

  return (
    <View style={style}>
      <Button
        {...button}
        loading={working}
        disabled={disabled || working}
        onPress={() => void press()}
        accessibilityState={{ busy: working, disabled: disabled || working }}
      >
        {working ? busyLabel : label}
      </Button>
      {slow && (
        <Text variant="bodySmall" style={[styles.slow, { color: theme.colors.onSurfaceVariant }]} accessibilityLiveRegion="polite">
          {SLOW_ACTION_TEXT}
        </Text>
      )}
    </View>
  )
}

const styles = StyleSheet.create({
  slow: { marginTop: 4, textAlign: 'center' },
})
