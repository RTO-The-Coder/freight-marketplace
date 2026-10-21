import { Pressable, ScrollView, StyleSheet, View } from 'react-native'
import { Checkbox, Text, useTheme } from 'react-native-paper'
import { useAppColors } from '../../theme'

interface SelectorTruck {
  truckId: string
  truckName: string
  /** The truck's route colour on the map. */
  color: string
}

interface Props {
  /** Every active truck, in fleet order. */
  trucks: SelectorTruck[]
  hiddenTruckIds: ReadonlySet<string>
  onToggleTruck: (truckId: string) => void
  onShowAll: () => void
  onShowNone: () => void
  hideAtOffice: boolean
  onHideAtOfficeChange: (hide: boolean) => void
}

/** Under the fleet map, as on the web: hide trucks at the office, and which active trucks it draws. */
export function TruckSelector({ trucks, hiddenTruckIds, onToggleTruck, onShowAll, onShowNone, hideAtOffice, onHideAtOfficeChange }: Props) {
  const theme = useTheme()
  const colors = useAppColors()
  return (
    <View style={styles.selector}>
      <Checkbox.Item
        label="Hide trucks at the office"
        status={hideAtOffice ? 'checked' : 'unchecked'}
        onPress={() => onHideAtOfficeChange(!hideAtOffice)}
        mode="android"
        position="leading"
        style={styles.check}
        labelStyle={styles.checkLabel}
      />

      {trucks.length > 0 && (
        <View style={styles.showRow}>
          <Text variant="labelMedium" style={{ color: theme.colors.onSurfaceVariant }}>
            Show on map:
          </Text>
          <Pressable onPress={onShowAll} hitSlop={8} accessibilityRole="button">
            <Text variant="labelLarge" style={{ color: theme.colors.primary }}>
              All
            </Text>
          </Pressable>
          <Text style={{ color: theme.colors.onSurfaceVariant }}>·</Text>
          <Pressable onPress={onShowNone} hitSlop={8} accessibilityRole="button">
            <Text variant="labelLarge" style={{ color: theme.colors.primary }}>
              None
            </Text>
          </Pressable>
        </View>
      )}

      {trucks.length > 0 && (
        <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.chips}>
          {trucks.map((truck) => {
            const shown = !hiddenTruckIds.has(truck.truckId)
            return (
              <Pressable
                key={truck.truckId}
                onPress={() => onToggleTruck(truck.truckId)}
                accessibilityRole="checkbox"
                accessibilityState={{ checked: shown }}
                accessibilityLabel={`Show ${truck.truckName} on the map`}
                style={[
                  styles.chip,
                  shown
                    ? { backgroundColor: colors.accentBg, borderColor: colors.accentBorder }
                    : { backgroundColor: theme.colors.surface, borderColor: colors.borderStrong },
                ]}
              >
                <View style={[styles.swatch, { backgroundColor: truck.color, opacity: shown ? 1 : 0.35 }]} />
                <Text variant="labelLarge" style={{ color: shown ? theme.colors.onSurface : theme.colors.onSurfaceVariant }}>
                  {truck.truckName}
                </Text>
              </Pressable>
            )
          })}
        </ScrollView>
      )}
    </View>
  )
}

const styles = StyleSheet.create({
  selector: { gap: 4 },
  check: { paddingHorizontal: 0, paddingVertical: 0 },
  checkLabel: { textAlign: 'left' },
  showRow: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  chips: { gap: 6, paddingVertical: 4 },
  chip: { flexDirection: 'row', alignItems: 'center', gap: 6, borderRadius: 999, borderWidth: 1, paddingHorizontal: 11, paddingVertical: 5 },
  swatch: { width: 10, height: 10, borderRadius: 5 },
})
