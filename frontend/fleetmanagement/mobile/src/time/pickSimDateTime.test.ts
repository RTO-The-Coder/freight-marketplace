import { DateTimePickerAndroid } from '@react-native-community/datetimepicker'
import { pickSimDateTime } from './pickSimDateTime'

jest.mock('@react-native-community/datetimepicker', () => ({
  DateTimePickerAndroid: { open: jest.fn() },
}))

const open = DateTimePickerAndroid.open as jest.Mock

type OpenArgs = {
  value: Date
  mode: string
  timeZoneName?: string
  is24Hour?: boolean
  onValueChange: (e: unknown, d: Date) => void
  onDismiss: () => void
}

function lastCall(): OpenArgs {
  return open.mock.calls[open.mock.calls.length - 1][0]
}

beforeEach(() => open.mockReset())

it('opens the date dialog, then the time dialog, both in UTC and 24h', async () => {
  const result = pickSimDateTime('2026-08-01T05:00:00Z')

  const dateArgs = lastCall()
  expect(dateArgs.mode).toBe('date')
  expect(dateArgs.timeZoneName).toBe('UTC')
  expect(dateArgs.is24Hour).toBe(true)
  expect(dateArgs.value.toISOString()).toBe('2026-08-01T05:00:00.000Z')
  dateArgs.onValueChange({}, new Date('2026-08-29T05:00:00Z'))

  await Promise.resolve()
  const timeArgs = lastCall()
  expect(timeArgs.mode).toBe('time')
  expect(timeArgs.timeZoneName).toBe('UTC')
  timeArgs.onValueChange({}, new Date('2026-08-29T14:20:00Z'))

  await expect(result).resolves.toBe('2026-08-29T14:20')
})

it('returns null when the date dialog is dismissed', async () => {
  const result = pickSimDateTime('2026-08-01T05:00:00Z')
  lastCall().onDismiss()
  await expect(result).resolves.toBeNull()
  expect(open).toHaveBeenCalledTimes(1)
})

it('returns null when the time dialog is dismissed', async () => {
  const result = pickSimDateTime('2026-08-01T05:00:00Z')
  lastCall().onValueChange({}, new Date('2026-08-29T05:00:00Z'))
  await Promise.resolve()
  lastCall().onDismiss()
  await expect(result).resolves.toBeNull()
})
