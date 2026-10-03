import type { DriverSummaryDto } from '@freight/api-client'
import { useMemo, useState } from 'react'
import { StyleSheet, View } from 'react-native'
import { Button, List, Text, TextInput, useTheme } from 'react-native-paper'
import { fullName, searchDrivers } from '@freight/fleetmanagement-core'

interface Props {
  label: string
  drivers: DriverSummaryDto[]
  value: string | null
  onChange: (driverId: string | null) => void
  /** The other slot's driver, never offered here. */
  excludeId: string | null
}

/** One driver slot: shows the chosen driver with "Change", or a search field with matching drivers. */
export function DriverPicker({ label, drivers, value, onChange, excludeId }: Props) {
  const theme = useTheme()
  const [query, setQuery] = useState('')
  const selected = value ? drivers.find((d) => d.driverId === value) ?? null : null
  const results = useMemo(() => searchDrivers(drivers, query, excludeId), [drivers, query, excludeId])

  if (selected) {
    return (
      <View style={styles.slot}>
        <Text variant="labelLarge">{label}</Text>
        <List.Item
          title={fullName(selected)}
          left={(props) => <List.Icon {...props} icon="account" />}
          right={() => (
            <Button
              compact
              onPress={() => {
                onChange(null)
                setQuery('')
              }}
              accessibilityLabel={`Change ${label.toLowerCase()}`}
            >
              Change
            </Button>
          )}
        />
      </View>
    )
  }

  return (
    <View style={styles.slot}>
      <Text variant="labelLarge">{label}</Text>
      <TextInput
        mode="outlined"
        placeholder="Search by name…"
        accessibilityLabel={`Search ${label.toLowerCase()}`}
        left={<TextInput.Icon icon="magnify" />}
        value={query}
        onChangeText={setQuery}
      />
      {results.list.length === 0 ? (
        <Text variant="bodyMedium" style={{ color: theme.colors.onSurfaceVariant }}>
          {query.trim() ? 'No drivers match' : 'No unassigned drivers'}
        </Text>
      ) : (
        results.list.map((d) => (
          <List.Item
            key={d.driverId}
            title={fullName(d)}
            left={(props) => <List.Icon {...props} icon="account-outline" />}
            onPress={() => onChange(d.driverId)}
          />
        ))
      )}
      {results.total > results.list.length && (
        <Text variant="bodySmall" style={{ color: theme.colors.onSurfaceVariant }}>
          {`+${results.total - results.list.length} more — keep typing`}
        </Text>
      )}
    </View>
  )
}

const styles = StyleSheet.create({
  slot: { gap: 4 },
})
