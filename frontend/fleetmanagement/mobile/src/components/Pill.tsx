import { StyleSheet, View } from 'react-native'
import { Text, useTheme } from 'react-native-paper'
import { useAppColors } from '../theme'

/** The web pill tones: plain grey, accent, and the status hues. */
export type PillTone = 'plain' | 'accent' | 'running' | 'idle' | 'office'

/** A small rounded label: a truck's type and size, "Inactive", "Assigned · Flatbed 1". */
export function Pill({ label, tone = 'plain' }: { label: string; tone?: PillTone }) {
  const theme = useTheme()
  const colors = useAppColors()
  const [text, background] = {
    plain: [theme.colors.onSurfaceVariant, theme.colors.surfaceVariant],
    accent: [theme.colors.primary, colors.accentBg],
    running: [colors.running, colors.runningBg],
    idle: [colors.idle, colors.idleBg],
    office: [colors.office, colors.officeBg],
  }[tone]
  return (
    <View style={[styles.pill, { backgroundColor: background }]}>
      <Text variant="labelMedium" style={{ color: text }}>
        {label}
      </Text>
    </View>
  )
}

const styles = StyleSheet.create({
  pill: { alignSelf: 'flex-start', borderRadius: 999, paddingHorizontal: 8, paddingVertical: 1 },
})
