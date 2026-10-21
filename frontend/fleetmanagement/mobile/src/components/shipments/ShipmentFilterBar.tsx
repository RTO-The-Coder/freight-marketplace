import type { TruckType } from '@freight/api-client'
import { useState } from 'react'
import { StyleSheet, View } from 'react-native'
import { Button, Menu, Text, useTheme } from 'react-native-paper'
import {
  datetimeLocalToIso,
  fmtSimShort,
  TRUCK_TYPE_LABELS,
  TRUCK_TYPES,
  type ShipmentFilters,
  type ShipmentSortKey,
  type SortDirection,
} from '@freight/fleetmanagement-core'
import { pickSimDateTime } from '../../time/pickSimDateTime'

const SORT_LABEL: Record<ShipmentSortKey, string> = {
  pickup: 'Pickup',
  delivery: 'Delivery',
  load: 'Load',
  type: 'Type',
  offersClose: 'Offers close',
}

type DateField = 'pickupFrom' | 'pickupTo' | 'deliveryFrom' | 'deliveryTo'
const DATE_FIELDS: readonly DateField[] = ['pickupFrom', 'pickupTo', 'deliveryFrom', 'deliveryTo']

interface Props {
  filters: ShipmentFilters
  onChange: (filters: ShipmentFilters) => void
  sortKey: ShipmentSortKey
  /** The keys this list can sort by. */
  sortKeys: readonly ShipmentSortKey[]
  onSortKeyChange: (key: ShipmentSortKey) => void
  sortDirection: SortDirection
  onSortDirectionChange: (direction: SortDirection) => void
  /** False for one truck: the type is the truck's, so there is nothing to pick. */
  showType?: boolean
}

/**
 * Above the shipment lists, as on the web: truck type, sort (and its direction), and Advanced
 * search - the pickup and delivery ranges, closed by default. Clear (dates only) shows while a date is set.
 */
export function ShipmentFilterBar({
  filters,
  onChange,
  sortKey,
  sortKeys,
  onSortKeyChange,
  sortDirection,
  onSortDirectionChange,
  showType = true,
}: Props) {
  const theme = useTheme()
  const [menu, setMenu] = useState<'type' | 'sort' | null>(null)
  const [advancedOpen, setAdvancedOpen] = useState(false)
  const set = (change: Partial<ShipmentFilters>) => onChange({ ...filters, ...change })
  const datesSet = DATE_FIELDS.filter((field) => filters[field] !== null).length

  const pickDate = async (field: DateField) => {
    const picked = await pickSimDateTime(filters[field])
    if (picked) set({ [field]: datetimeLocalToIso(picked) })
  }

  const dateButton = (field: DateField, label: string) => (
    <Button
      mode="outlined"
      compact
      style={styles.date}
      onPress={() => void pickDate(field)}
      accessibilityLabel={label}
    >
      {filters[field] ? fmtSimShort(filters[field]) : 'Any'}
    </Button>
  )

  return (
    <View style={styles.bar} accessibilityLabel="Shipment filters">
      <View style={styles.line}>
        {showType && (
          <Menu
            visible={menu === 'type'}
            onDismiss={() => setMenu(null)}
            anchor={
              <Button mode="outlined" compact icon="chevron-down" contentStyle={styles.dropdown} onPress={() => setMenu('type')}>
                {`Type: ${filters.truckType ? TRUCK_TYPE_LABELS[filters.truckType] : 'All'}`}
              </Button>
            }
          >
            <Menu.Item
              title="All"
              onPress={() => {
                set({ truckType: null })
                setMenu(null)
              }}
            />
            {TRUCK_TYPES.map((t: TruckType) => (
              <Menu.Item
                key={t}
                title={TRUCK_TYPE_LABELS[t]}
                onPress={() => {
                  set({ truckType: t })
                  setMenu(null)
                }}
              />
            ))}
          </Menu>
        )}

        <Menu
          visible={menu === 'sort'}
          onDismiss={() => setMenu(null)}
          anchor={
            <Button mode="outlined" compact icon="chevron-down" contentStyle={styles.dropdown} onPress={() => setMenu('sort')}>
              {`Sort: ${SORT_LABEL[sortKey]}`}
            </Button>
          }
        >
          {sortKeys.map((key) => (
            <Menu.Item
              key={key}
              title={SORT_LABEL[key]}
              onPress={() => {
                onSortKeyChange(key)
                setMenu(null)
              }}
            />
          ))}
        </Menu>

        <Button
          mode="outlined"
          compact
          onPress={() => onSortDirectionChange(sortDirection === 'asc' ? 'desc' : 'asc')}
          accessibilityLabel={
            sortDirection === 'asc' ? 'Sorted ascending - switch to descending' : 'Sorted descending - switch to ascending'
          }
        >
          {sortDirection === 'asc' ? '↑' : '↓'}
        </Button>
      </View>

      <View style={styles.line}>
        <Button mode="text" compact onPress={() => setAdvancedOpen((o) => !o)} accessibilityState={{ expanded: advancedOpen }}>
          {`Advanced search${datesSet > 0 ? ` (${datesSet})` : ''} ${advancedOpen ? '▾' : '▸'}`}
        </Button>
        {datesSet > 0 && (
          <Button mode="text" compact onPress={() => set({ pickupFrom: null, pickupTo: null, deliveryFrom: null, deliveryTo: null })}>
            Clear
          </Button>
        )}
      </View>

      {advancedOpen && (
        <View style={[styles.dates, { backgroundColor: theme.colors.surfaceVariant }]}>
          <Text variant="labelMedium">Pickup</Text>
          <View style={styles.range}>
            {dateButton('pickupFrom', 'Pickup from')}
            <Text style={{ color: theme.colors.onSurfaceVariant }}>–</Text>
            {dateButton('pickupTo', 'Pickup to')}
          </View>
          <Text variant="labelMedium">Delivery</Text>
          <View style={styles.range}>
            {dateButton('deliveryFrom', 'Delivery from')}
            <Text style={{ color: theme.colors.onSurfaceVariant }}>–</Text>
            {dateButton('deliveryTo', 'Delivery to')}
          </View>
          <Text variant="bodySmall" style={{ color: theme.colors.onSurfaceVariant }}>
            Times are simulation-clock times (UTC). A shipment shows if its window overlaps the range.
          </Text>
        </View>
      )}
    </View>
  )
}

const styles = StyleSheet.create({
  bar: { gap: 2 },
  line: { flexDirection: 'row', flexWrap: 'wrap', alignItems: 'center', gap: 6 },
  // The chevron after the text, like a dropdown.
  dropdown: { flexDirection: 'row-reverse' },
  dates: { borderRadius: 12, padding: 10, gap: 6 },
  range: { flexDirection: 'row', alignItems: 'center', gap: 6 },
  date: { flex: 1 },
})
