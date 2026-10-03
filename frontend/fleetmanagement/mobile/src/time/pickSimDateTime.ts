import { DateTimePickerAndroid } from '@react-native-community/datetimepicker'
import { isoToDatetimeLocal } from '@freight/fleetmanagement-core'

type Mode = 'date' | 'time'

function openPicker(value: Date, mode: Mode): Promise<Date | null> {
  return new Promise((resolve) => {
    DateTimePickerAndroid.open({
      value,
      mode,
      is24Hour: true,
      // Simulation time is a UTC wall clock: show and pick it in UTC, never the phone's zone.
      timeZoneName: 'UTC',
      onValueChange: (_event, date) => resolve(date),
      onDismiss: () => resolve(null),
    })
  })
}

/**
 * Android's date dialog, then its time dialog, both in UTC. Resolves the picked
 * simulation time as "YYYY-MM-DDTHH:mm" (what core's SimClock.setTime and
 * datetimeLocalToIso expect), or null if either dialog is dismissed.
 */
export async function pickSimDateTime(initialIso: string | null): Promise<string | null> {
  const initial = initialIso ? new Date(initialIso) : new Date()
  const date = await openPicker(initial, 'date')
  if (!date) return null
  const dateTime = await openPicker(date, 'time')
  if (!dateTime) return null
  return isoToDatetimeLocal(dateTime.toISOString())
}
