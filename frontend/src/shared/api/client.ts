import { sesionGuardada } from './sesionGuardada'
import type {
  CrearUsuarioRequest,
  HealthStatus,
  LoginRequest,
  ProblemDetails,
  Sesion,
  TenantPublico,
  Usuario,
} from './types'

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
    super(problem?.detail ?? problem?.title ?? `La API respondió ${status}`)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }

  /** Errores de validación por campo, tal como los devuelve `ValidationProblemDetails`. */
  get erroresPorCampo(): Record<string, string[]> {
    return this.problem?.errors ?? {}
  }
}

type Metodo = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'

interface RequestOptions {
  method?: Metodo
  body?: unknown
  signal?: AbortSignal
  /** Los endpoints de sesión no se reintentan: son los que producen el token. */
  sinReintento?: boolean
}

/** Se avisa cuando la sesión se cae, para que la UI mande al login. */
type Escucha = (sesion: Sesion | null) => void

let sesionActual: Sesion | null = sesionGuardada.leer()
const escuchas = new Set<Escucha>()

/**
 * Un único refresco en vuelo. Sin esto, cinco requests que vencen a la vez disparan cinco
 * refrescos, y como el refresh token rota en cada uso, cuatro de ellos llegan con un token
 * ya quemado: el servidor lo lee como reuso y cierra todas las sesiones del usuario.
 */
let refrescoEnCurso: Promise<boolean> | null = null

function publicar(sesion: Sesion | null) {
  sesionActual = sesion
  for (const escucha of escuchas) escucha(sesion)
}

export const sesion = {
  actual: () => sesionActual,

  suscribirse(escucha: Escucha): () => void {
    escuchas.add(escucha)
    return () => escuchas.delete(escucha)
  },

  establecer(nueva: Sesion) {
    sesionGuardada.guardar(nueva)
    publicar(nueva)
  },

  limpiar() {
    sesionGuardada.borrar()
    publicar(null)
  },
}

async function enviar(path: string, options: RequestOptions, token: string | null) {
  const { method = 'GET', body, signal } = options

  const headers: Record<string, string> = {}
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  if (token) headers.Authorization = `Bearer ${token}`

  return fetch(`${baseUrl}${path}`, {
    method,
    signal,
    headers: Object.keys(headers).length === 0 ? undefined : headers,
    body: body === undefined ? undefined : JSON.stringify(body),
  })
}

async function request<TResponse>(path: string, options: RequestOptions = {}): Promise<TResponse> {
  let response = await enviar(path, options, sesionActual?.accessToken ?? null)

  // Un 401 con sesión abierta casi siempre es un access token vencido: se renueva una vez
  // y se reintenta. Si el refresh tampoco sirve, la sesión se cierra de verdad.
  if (response.status === 401 && !options.sinReintento && sesionActual) {
    const renovada = await refrescar()

    if (renovada) {
      response = await enviar(path, options, sesionActual?.accessToken ?? null)
    } else {
      sesion.limpiar()
    }
  }

  if (!response.ok) {
    throw new ApiError(response.status, await leerProblemDetails(response))
  }

  if (response.status === 204) {
    return undefined as TResponse
  }

  return (await response.json()) as TResponse
}

async function refrescar(): Promise<boolean> {
  refrescoEnCurso ??= (async () => {
    try {
      const refreshToken = sesionActual?.refreshToken
      if (!refreshToken) return false

      const response = await enviar(
        '/api/auth/refresh',
        { method: 'POST', body: { refreshToken } },
        null,
      )

      if (!response.ok) return false

      sesion.establecer((await response.json()) as Sesion)
      return true
    } catch {
      return false
    } finally {
      refrescoEnCurso = null
    }
  })()

  return refrescoEnCurso
}

async function leerProblemDetails(response: Response): Promise<ProblemDetails | null> {
  try {
    return (await response.json()) as ProblemDetails
  } catch {
    return null
  }
}

/**
 * En desarrollo el tenant del sitio público viaja como prefijo de la ruta. En producción,
 * cuando cada automotora entra por su dominio, el prefijo sobra y el servidor resuelve el
 * tenant desde el `Host`.
 */
function rutaPublica(slug: string | null, path: string): string {
  return slug ? `/t/${slug}${path}` : path
}

export const api = {
  baseUrl,

  health: (signal?: AbortSignal) => request<HealthStatus>('/api/health', { signal }),

  auth: {
    login: (credenciales: LoginRequest) =>
      request<Sesion>('/api/auth/login', {
        method: 'POST',
        body: credenciales,
        sinReintento: true,
      }),

    me: (signal?: AbortSignal) => request<Usuario>('/api/auth/me', { signal }),

    logout: (refreshToken: string) =>
      request<void>('/api/auth/logout', {
        method: 'POST',
        body: { refreshToken },
        sinReintento: true,
      }),
  },

  usuarios: {
    listar: (signal?: AbortSignal) => request<Usuario[]>('/api/users', { signal }),

    crear: (nuevo: CrearUsuarioRequest) =>
      request<Usuario>('/api/users', { method: 'POST', body: nuevo }),

    actualizar: (id: number, cambios: { nombre: string; activo: boolean }) =>
      request<Usuario>(`/api/users/${id}`, { method: 'PUT', body: cambios }),
  },

  publico: {
    tenant: (slug: string | null, signal?: AbortSignal) =>
      request<TenantPublico>(rutaPublica(slug, '/api/public/tenant'), { signal }),
  },
}
