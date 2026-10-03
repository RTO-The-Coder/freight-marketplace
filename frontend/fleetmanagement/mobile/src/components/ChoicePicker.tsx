import { StyleSheet, View } from 'react-native'
import { Chip, Text, useTheme } from 'react-native-paper'

interface Props<T extends string> {
  label: string
  options: readonly T[]
  value: T | null
  onChange: (value: T) => void
  /** Extra text shown under the options for the selected value. */
  hint?: (value: T) => string
}

/** Tap-to-select chips — the mobile counterpart of the web Picker. */
export function ChoicePicker<T extends string>({ label, options, value, onChange, hint }: Props<T>) {
  const theme = useTheme()
  return (
    <View style={styles.field}>
      <Text variant="labelLarge">{label}</Text>
      <View style={styles.options}>
        {options.map((option) => (
          <Chip key={option} selected={option === value} showSelectedOverlay onPress={() => onChange(option)}>
            {option}
          </Chip>
        ))}
      </View>
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
})
