import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiError, createApiClient, onRequestsInFlightChange } from './client'

/** A fetch that stays pending until the test answers it. */
function pendingFetch() {
  const answers: ((response: Response) => void)[] = []
  vi.stubGlobal('fetch', () => new Promise<Response>((resolve) => answers.push(resolve)))
  return answers
}

const ok = () => new Response(JSON.stringify({ fine: true }), { status: 200 })
const failed = () => new Response(JSON.stringify({ error: 'Truck not found.' }), { status: 404 })

describe('requests in flight', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('counts each request from sent to answered', async () => {
    const answers = pendingFetch()
    const counts: number[] = []
    const unsubscribe = onRequestsInFlightChange((count) => counts.push(count))
    const client = createApiClient({ baseUrl: 'http://api' })

    const first = client.get('/a')
    const second = client.post('/b', { x: 1 })
    expect(counts).toEqual([1, 2])

    answers[0](ok())
    await first
    expect(counts).toEqual([1, 2, 1])

    answers[1](ok())
    await second
    expect(counts).toEqual([1, 2, 1, 0])
    unsubscribe()
  })

  it('counts a failed request as finished too', async () => {
    const answers = pendingFetch()
    const counts: number[] = []
    const unsubscribe = onRequestsInFlightChange((count) => counts.push(count))

    const request = createApiClient({ baseUrl: 'http://api' }).get('/trucks/1')
    answers[0](failed())
    await expect(request).rejects.toBeInstanceOf(ApiError)
    expect(counts).toEqual([1, 0])
    unsubscribe()
  })

  it('counts a request whose network call throws as finished', async () => {
    vi.stubGlobal('fetch', () => Promise.reject(new TypeError('Network request failed')))
    const counts: number[] = []
    const unsubscribe = onRequestsInFlightChange((count) => counts.push(count))

    await expect(createApiClient({ baseUrl: 'http://api' }).get('/a')).rejects.toThrow('Network request failed')
    expect(counts).toEqual([1, 0])
    unsubscribe()
  })

  it('stops calling a listener once it unsubscribes', async () => {
    const answers = pendingFetch()
    const listener = vi.fn()
    onRequestsInFlightChange(listener)()

    const request = createApiClient({ baseUrl: 'http://api' }).get('/a')
    answers[0](ok())
    await request
    expect(listener).not.toHaveBeenCalled()
  })
})
