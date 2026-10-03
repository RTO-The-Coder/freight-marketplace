import { act, renderHook, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { SimClockProvider, useSimClock } from './SimClock'

const { getTime, advance, setTime } = vi.hoisted(() => ({
  getTime: vi.fn(),
  advance: vi.fn(),
  setTime: vi.fn(),
}))

vi.mock('../api/apiClient', () => ({
  simulationApi: { getTime, advance, setTime },
}))

const wrapper = ({ children }: { children: ReactNode }) => <SimClockProvider>{children}</SimClockProvider>

beforeEach(() => {
  getTime.mockReset().mockResolvedValue({ currentTime: '2026-08-01T05:00:00Z' })
  advance.mockReset()
  setTime.mockReset()
})

describe('SimClockProvider', () => {
  it('loads the current simulation time on mount', async () => {
    const { result } = renderHook(() => useSimClock(), { wrapper })
    await waitFor(() => expect(result.current.currentTime).toBe('2026-08-01T05:00:00Z'))
    expect(result.current.simVersion).toBe(0)
    expect(result.current.error).toBeNull()
  })

  it('advances the clock, records the result and bumps simVersion', async () => {
    advance.mockResolvedValue({ currentTime: '2026-08-01T06:00:00Z', tripsAdvanced: 2, tripsCompleted: 1 })
    const { result } = renderHook(() => useSimClock(), { wrapper })
    await waitFor(() => expect(result.current.currentTime).not.toBeNull())

    await act(() => result.current.advance(12))

    expect(advance).toHaveBeenCalledWith(12)
    expect(result.current.currentTime).toBe('2026-08-01T06:00:00Z')
    expect(result.current.lastAdvance).toEqual({ tripsAdvanced: 2, tripsCompleted: 1 })
    expect(result.current.simVersion).toBe(1)
    expect(result.current.busy).toBe(false)
  })

  it('ignores an advance of zero ticks', async () => {
    const { result } = renderHook(() => useSimClock(), { wrapper })
    await act(() => result.current.advance(0))
    expect(advance).not.toHaveBeenCalled()
  })

  it('sets the time as a UTC wall-clock instant and clears the last advance', async () => {
    setTime.mockResolvedValue({ currentTime: '2026-08-29T14:20:00Z' })
    const { result } = renderHook(() => useSimClock(), { wrapper })
    await waitFor(() => expect(result.current.currentTime).not.toBeNull())

    await act(() => result.current.setTime('2026-08-29T14:20'))

    expect(setTime).toHaveBeenCalledWith('2026-08-29T14:20:00Z')
    expect(result.current.currentTime).toBe('2026-08-29T14:20:00Z')
    expect(result.current.lastAdvance).toBeNull()
    expect(result.current.simVersion).toBe(1)
  })

  it('surfaces an error message when the API fails', async () => {
    advance.mockRejectedValue(new Error('Server down'))
    const { result } = renderHook(() => useSimClock(), { wrapper })
    await waitFor(() => expect(result.current.currentTime).not.toBeNull())

    await act(() => result.current.advance(1))

    expect(result.current.error).toBe('Server down')
    expect(result.current.busy).toBe(false)
    expect(result.current.simVersion).toBe(0)
  })
})

describe('useSimClock', () => {
  it('throws outside a provider', () => {
    vi.spyOn(console, 'error').mockImplementation(() => {})
    expect(() => renderHook(() => useSimClock())).toThrow('useSimClock must be used within a SimClockProvider')
  })
})
