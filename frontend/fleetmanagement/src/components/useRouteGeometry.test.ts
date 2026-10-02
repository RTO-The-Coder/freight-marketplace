import { renderHook, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useRouteGeometry, type GeoStop } from './useRouteGeometry'

const { geometryMock } = vi.hoisted(() => ({ geometryMock: vi.fn() }))

vi.mock('../apiClient', () => ({
  routingApi: { geometry: geometryMock },
}))

function stop(sequence: number, lat: number, lng: number): GeoStop {
  return { sequence, latitude: lat, longitude: lng }
}

function fakeGeometry(lat: number, lng: number) {
  return { distanceKm: 10, timeTicks: 2, path: [{ lat, lng }] }
}

beforeEach(() => {
  geometryMock.mockReset()
  // The module-level geometry cache is keyed on coordinates rounded to 4dp —
  // use distinct coordinates per test so cache state from an earlier test in
  // this file can never leak into another's assertions.
})

describe('useRouteGeometry — single chain', () => {
  it('returns no legs and does not call the API for fewer than 2 stops', async () => {
    const { result } = renderHook(() => useRouteGeometry([stop(1, 10, 10)]))
    await waitFor(() => expect(result.current.legs).toEqual([]))
    expect(geometryMock).not.toHaveBeenCalled()
  })

  it('fetches one leg per consecutive stop pair, in sequence order', async () => {
    geometryMock.mockImplementation((fromLat: number) => Promise.resolve(fakeGeometry(fromLat, 0)))

    const stops = [stop(2, 21, 21), stop(1, 20, 20), stop(3, 22, 22)] // deliberately unsorted
    const { result } = renderHook(() => useRouteGeometry(stops))

    await waitFor(() => expect(result.current.legs).not.toBeNull())

    expect(geometryMock).toHaveBeenCalledTimes(2)
    // Called in sequence order (20->21, then 21->22), not array order.
    expect(geometryMock).toHaveBeenNthCalledWith(1, 20, 20, 21, 21)
    expect(geometryMock).toHaveBeenNthCalledWith(2, 21, 21, 22, 22)
    expect(result.current.anyStraightLine).toBe(false)
  })

  it('prepends an origin leg when origin is given', async () => {
    geometryMock.mockImplementation((fromLat: number) => Promise.resolve(fakeGeometry(fromLat, 0)))

    const origin: GeoStop = { sequence: -1, latitude: 30, longitude: 30 }
    const stops = [stop(1, 31, 31)]
    const { result } = renderHook(() => useRouteGeometry(stops, origin))

    await waitFor(() => expect(result.current.legs).not.toBeNull())

    expect(result.current.legs).toHaveLength(1)
    expect(result.current.legs![0].isOriginLeg).toBe(true)
    expect(geometryMock).toHaveBeenCalledWith(30, 30, 31, 31)
  })

  it('falls back to a straight line and flags anyStraightLine when a leg fails', async () => {
    geometryMock.mockRejectedValue(new Error('routing unavailable'))

    const stops = [stop(1, 40, 40), stop(2, 41, 41)]
    const { result } = renderHook(() => useRouteGeometry(stops))

    await waitFor(() => expect(result.current.legs).not.toBeNull())

    expect(result.current.anyStraightLine).toBe(true)
    expect(result.current.legs![0].road).toBe(false)
    expect(result.current.legs![0].path).toEqual([
      [40, 40],
      [41, 41],
    ])
  })

  it('resolves a cache hit without calling the API again', async () => {
    geometryMock.mockResolvedValue(fakeGeometry(50, 50))

    const stops = [stop(1, 50, 50), stop(2, 51, 51)]
    const { result, rerender } = renderHook((props: GeoStop[]) => useRouteGeometry(props), {
      initialProps: stops,
    })
    await waitFor(() => expect(result.current.legs).not.toBeNull())
    expect(geometryMock).toHaveBeenCalledTimes(1)

    // Re-render with a new array of the same coordinates — same logical route.
    rerender([stop(1, 50, 50), stop(2, 51, 51)])
    await waitFor(() => expect(result.current.legs).not.toBeNull())

    expect(geometryMock).toHaveBeenCalledTimes(1) // no second network call
  })
})

describe('useRouteGeometry — multi-chain (fleet map)', () => {
  it('keeps each chain independent — no leg connects one truck to another', async () => {
    geometryMock.mockImplementation((fromLat: number) => Promise.resolve(fakeGeometry(fromLat, 0)))

    const chains = [
      { stops: [stop(1, 60, 60), stop(2, 61, 61)] },
      { stops: [stop(1, 70, 70), stop(2, 71, 71)] },
    ]
    const { result } = renderHook(() => useRouteGeometry(chains))

    await waitFor(() => expect(result.current.legs).not.toBeNull())

    expect(result.current.legs).toHaveLength(2)
    expect(geometryMock).toHaveBeenCalledWith(60, 60, 61, 61)
    expect(geometryMock).toHaveBeenCalledWith(70, 70, 71, 71)
    // Never called with a cross-chain pair.
    expect(geometryMock).not.toHaveBeenCalledWith(61, 61, 70, 70)
  })

  it('applies a per-chain origin only to that chain', async () => {
    geometryMock.mockImplementation((fromLat: number) => Promise.resolve(fakeGeometry(fromLat, 0)))

    const chains = [
      { stops: [stop(1, 80, 80)], origin: { sequence: -1, latitude: 79, longitude: 79 } },
      { stops: [stop(1, 90, 90)] }, // no origin — no leg at all for this chain
    ]
    const { result } = renderHook(() => useRouteGeometry(chains))

    await waitFor(() => expect(result.current.legs).not.toBeNull())

    expect(result.current.legs).toHaveLength(1)
    expect(result.current.legs![0].isOriginLeg).toBe(true)
    expect(geometryMock).toHaveBeenCalledWith(79, 79, 80, 80)
  })
})
