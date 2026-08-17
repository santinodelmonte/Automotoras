import type { HealthStatus, ProblemDetails } from './types'

const baseUrl = import.meta.env.VITE_API_BASE_URL

if (!baseUrl) {
  throw new Error(
    'Falta VITE_API_BASE_URL. Copiá .env.example a .env y completá la URL de la API.',
  )
}

/** Error tipado de la API. Lleva el status HTTP y el ProblemDetails si vino. */
export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails | null

  constructor(status: number, problem: ProblemDetails | null) {
    super(problem?.title ?? `La API respondió ${status}`)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }
}

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'
  body?: unknown
  signal?: AbortSignal
}

async function request<TResponse>(
  path: string,
  options: RequestOptions = {},
): Promise<TResponse> {
  const { method = 'GET', body, signal } = options

  // El interceptor de JWT y el refresh en 401 se agregan cuando exista autenticación.
  const response = await fetch(`${baseUrl}${path}`, {
    method,
    signal,
    headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  })

  if (!response.ok) {
    throw new ApiError(response.status, await readProblemDetails(response))
  }

  if (response.status === 204) {
    return undefined as TResponse
  }

  return (await response.json()) as TResponse
}

async function readProblemDetails(response: Response): Promise<ProblemDetails | null> {
  try {
    return (await response.json()) as ProblemDetails
  } catch {
    return null
  }
}

export const api = {
  baseUrl,
  health: (signal?: AbortSignal) => request<HealthStatus>('/api/health', { signal }),
}
