import { expect, it } from 'vitest'
import { TRUCK_STATUS_LABELS } from './truckStatus'

it('labels every truck status the way the web app shows it', () => {
  expect(TRUCK_STATUS_LABELS).toEqual({ Running: 'Running', Idle: 'Idle', AtOffice: 'At office' })
})
