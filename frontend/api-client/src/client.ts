export interface ApiClientConfig {
  baseUrl: string
}

export class ApiError extends Error {
  readonly status: number

  constructor(message: string, status: number) {
    super(message)
    this.status = status
  }
}

/** Requests sent and not yet answered, across every client - what an app's busy indicator follows. */
let requestsInFlight = 0
const inFlightListeners = new Set<(count: number) => void>()

function changeRequestsInFlight(delta: number) {
  requestsInFlight += delta
  for (const listener of inFlightListeners) listener(requestsInFlight)
}

/**
 * Calls `listener` with the number of requests in flight whenever it changes (optional: an app
 * that shows no busy indicator never subscribes). Returns the unsubscribe function.
 */
export function onRequestsInFlightChange(listener: (count: number) => void): () => void {
  inFlightListeners.add(listener)
  return () => {
    inFlightListeners.delete(listener)
  }
}

export function createApiClient(config: ApiClientConfig) {
  async function request<TResponse>(path: string, init?: RequestInit): Promise<TResponse> {
    changeRequestsInFlight(1)
    try {
      return await send<TResponse>(path, init)
    } finally {
      changeRequestsInFlight(-1)
    }
  }

  async function send<TResponse>(path: string, init?: RequestInit): Promise<TResponse> {
    const response = await fetch(`${config.baseUrl}${path}`, {
      ...init,
      headers: {
        'Content-Type': 'application/json',
        ...init?.headers,
      },
    })

    if (!response.ok) {
      const body = (await response.json().catch(() => null)) as { error?: string } | null
      throw new ApiError(body?.error ?? response.statusText, response.status)
    }

    if (response.status === 204) {
      return undefined as TResponse
    }

    return (await response.json()) as TResponse
  }

  return {
    get: <TResponse>(path: string) => request<TResponse>(path),
    post: <TResponse>(path: string, body?: unknown) =>
      request<TResponse>(path, { method: 'POST', body: body ? JSON.stringify(body) : undefined }),
    patch: <TResponse>(path: string, body?: unknown) =>
      request<TResponse>(path, { method: 'PATCH', body: body ? JSON.stringify(body) : undefined }),
    delete: <TResponse>(path: string, body?: unknown) =>
      request<TResponse>(path, { method: 'DELETE', body: body ? JSON.stringify(body) : undefined }),
  }
}

export type ApiClient = ReturnType<typeof createApiClient>
