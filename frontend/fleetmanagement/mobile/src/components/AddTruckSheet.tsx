import { ApiError, type TruckSize, type TruckType } from '@freight/api-client'
import { useState } from 'react'
import { StyleSheet } from 'react-native'
import { Button, Text, TextInput, useTheme } from 'react-native-paper'
import {
  fleetApi,
  TRUCK_SIZES,
  TRUCK_TYPE_DESCRIPTIONS,
  TRUCK_TYPE_LABELS,
  TRUCK_TYPES,
  truckSizeCapacity,
  truckSummaryLine,
} from '@freight/fleetmanagement-core'
import { ActionButton } from './ActionButton'
import { ChoicePicker } from './ChoicePicker'
import { Sheet } from './Sheet'

interface Props {
  /** The new truck is created, then assigned to this company. */
  companyId: string
  /** Shown under the title. */
  companyName: string
  visible: boolean
  onClose: () => void
  /** The new truck, and its one-line summary for the confirmation ("FL-14 · Flatbed · Large"). */
  onAdded: (truckId: string, summary: string) => void
}

/** The quick Add a truck, as the web popup: name, type and size only - no defaults for type and size. */
export function AddTruckSheet({ companyId, companyName, visible, onClose, onAdded }: Props) {
  const theme = useTheme()
  const [truckName, setTruckName] = useState('')
  const [truckType, setTruckType] = useState<TruckType | null>(null)
  const [truckSize, setTruckSize] = useState<TruckSize | null>(null)
  const [error, setError] = useState<string | null>(null)

  const canSave = truckName.trim().length > 0 && truckType !== null && truckSize !== null

  const save = async () => {
    if (!canSave || truckType === null || truckSize === null) return
    setError(null)
    try {
      const name = truckName.trim()
      const { truckId } = await fleetApi.addTruck({ truckName: name, truckType, truckSize })
      await fleetApi.assignTruckToCompany(truckId, companyId)
      setTruckName('')
      setTruckType(null)
      setTruckSize(null)
      onAdded(truckId, truckSummaryLine({ truckName: name, truckType, truckSize }))
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to add truck.')
    }
  }

  return (
    <Sheet
      title="Add a truck"
      context={companyName}
      visible={visible}
      onClose={onClose}
      footer={
        <>
          <Button mode="outlined" style={styles.action} onPress={onClose}>
            Cancel
          </Button>
          <ActionButton
            style={styles.action}
            mode="contained"
            label="Add truck"
            busyLabel="Adding…"
            disabled={!canSave}
            onPress={save}
          />
        </>
      }
    >
      <TextInput
        mode="outlined"
        label="Truck name"
        accessibilityLabel="Truck name"
        placeholder="e.g. FL-14"
        value={truckName}
        onChangeText={setTruckName}
      />
      <ChoicePicker
        label="Type"
        options={TRUCK_TYPES}
        value={truckType}
        onChange={setTruckType}
        labelOf={(type) => TRUCK_TYPE_LABELS[type]}
        description={(type) => TRUCK_TYPE_DESCRIPTIONS[type]}
      />
      <ChoicePicker label="Size" options={TRUCK_SIZES} value={truckSize} onChange={setTruckSize} description={truckSizeCapacity} />
      {error && (
        <Text variant="bodyMedium" style={{ color: theme.colors.error }} accessibilityRole="alert">
          {error}
        </Text>
      )}
    </Sheet>
  )
}

const styles = StyleSheet.create({
  action: { flex: 1 },
})
