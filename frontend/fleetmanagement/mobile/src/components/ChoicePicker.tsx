import { Pressable, StyleSheet, View } from 'react-native'
import { Chip, Text, useTheme } from 'react-native-paper'
import { useAppColors } from '../theme'

interface Props<T extends string> {
  label: string
  options: readonly T[]
  value: T | null
  onChange: (value: T) => void
  /** The option in plain words ("Box van" for BoxVan); the value itself when left out. */
  labelOf?: (value: T) => string
  /**
   * A short description under each option ("closed, dry goods", "24,000 kg · 90 m³"). Given,
   * the options are cards, two to a row.
   */
  description?: (value: T) => string
  /** Extra text shown under the options for the selected value. */
  hint?: (value: T) => string
}

/** Tap-to-select options, nothing chosen until tapped — the mobile counterpart of the web Picker. */
export function ChoicePicker<T extends string>({ label, options, value, onChange, labelOf, description, hint }: Props<T>) {
  const theme = useTheme()
  const colors = useAppColors()
  const text = (option: T) => (labelOf ? labelOf(option) : option)

  return (
    <View style={styles.field} accessibilityRole="radiogroup" accessibilityLabel={label}>
      <Text variant="labelLarge">{label}</Text>
      {description ? (
        <View style={styles.cards}>
          {options.map((option) => {
            const selected = option === value
            return (
              <Pressable
                key={option}
                onPress={() => onChange(option)}
                accessibilityRole="radio"
                accessibilityState={{ selected }}
                accessibilityLabel={`${text(option)}, ${description(option)}`}
                style={[
                  styles.card,
                  selected
                    ? { borderColor: theme.colors.primary, backgroundColor: colors.accentBg }
                    : { borderColor: colors.borderStrong, backgroundColor: theme.colors.background },
                ]}
              >
                <Text variant="titleSmall">{text(option)}</Text>
                <Text variant="bodySmall" style={{ color: theme.colors.onSurfaceVariant }}>
                  {description(option)}
                </Text>
              </Pressable>
            )
          })}
        </View>
      ) : (
        <View style={styles.options}>
          {options.map((option) => (
            <Chip key={option} selected={option === value} showSelectedOverlay onPress={() => onChange(option)}>
              {text(option)}
            </Chip>
          ))}
        </View>
      )}
      {hint && value && (
        <Text variant="bodySmall" style={{ color: theme.colors.onSurfaceVariant }}>
          {hint(value)}
        </Text>
      )}
    </View>
  )
}

const styles = StyleSheet.create({
  field: { gap: 6 },
  options: { flexDirection: 'row', flexWrap: 'wrap', gap: 6 },
  cards: { flexDirection: 'row', flexWrap: 'wrap', gap: 6 },
  // Two to a row: half the width minus half the gap.
  card: { flexBasis: '48%', flexGrow: 1, borderWidth: 1, borderRadius: 12, paddingHorizontal: 10, paddingVertical: 8, gap: 1 },
})
