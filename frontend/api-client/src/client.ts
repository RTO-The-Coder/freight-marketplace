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

export function createApiClient(config: ApiClientConfig) {
  async function request<TResponse>(path: string, init?: RequestInit): Promise<TResponse> {
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
    delete: <TResponse>(path: string) => request<TResponse>(path, { method: 'DELETE' }),
  }
}

export type ApiClient = ReturnType<typeof createApiClient>
