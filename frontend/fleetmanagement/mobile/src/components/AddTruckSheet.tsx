import { ApiError, CAPACITY_BY_SIZE, type TruckSize, type TruckType } from '@freight/api-client'
import { useState } from 'react'
import { TextInput } from 'react-native-paper'
import { fleetApi } from '@freight/fleetmanagement-core'
import { ChoicePicker } from './ChoicePicker'
import { FormSheet } from './FormSheet'

const TRUCK_TYPES: readonly TruckType[] = ['BoxVan', 'Flatbed', 'Refrigerated', 'Tanker']
const TRUCK_SIZES: readonly TruckSize[] = ['Small', 'Medium', 'Large']

interface Props {
  /** The new truck is created, then assigned to this company. */
  companyId: string
  visible: boolean
  onClose: () => void
  onAdded: () => void
}

export function AddTruckSheet({ companyId, visible, onClose, onAdded }: Props) {
  const [truckName, setTruckName] = useState('')
  const [truckType, setTruckType] = useState<TruckType | null>(null)
  const [truckSize, setTruckSize] = useState<TruckSize | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const canSave = truckName.trim().length > 0 && truckType !== null && truckSize !== null

  const save = async () => {
    if (!canSave || truckType === null || truckSize === null) return
    setError(null)
    setBusy(true)
    try {
      const { truckId } = await fleetApi.addTruck({ truckName: truckName.trim(), truckType, truckSize })
      await fleetApi.assignTruckToCompany(truckId, companyId)
      setTruckName('')
      setTruckType(null)
      setTruckSize(null)
      onAdded()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to add truck.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <FormSheet
      title="Add Truck"
      visible={visible}
      onClose={onClose}
      confirmLabel="Save"
      busyLabel="Saving…"
      busy={busy}
      canConfirm={canSave}
      onConfirm={() => void save()}
      error={error}
    >
      <TextInput
        mode="outlined"
        label="Truck name"
        accessibilityLabel="Truck name"
        placeholder="e.g. FL-14"
        value={truckName}
        onChangeText={setTruckName}
      />
      <ChoicePicker label="Type" options={TRUCK_TYPES} value={truckType} onChange={setTruckType} />
      <ChoicePicker
        label="Size"
        options={TRUCK_SIZES}
        value={truckSize}
        onChange={setTruckSize}
        hint={(size) =>
          `Capacity ${CAPACITY_BY_SIZE[size].weightKg.toLocaleString()} kg · ${CAPACITY_BY_SIZE[size].volumeCubicMeters} m³`
        }
      />
    </FormSheet>
  )
}
