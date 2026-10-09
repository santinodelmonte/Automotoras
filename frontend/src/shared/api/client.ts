import { sesionGuardada } from './sesionGuardada'
import type {
  ActualizarTenantRequest,
  Benchmark,
  CambiarEstadoRequest,
  ConfiguracionDeTenant,
  CrearTenantRequest,
  FilaDeCobranza,
  GuardarPlanRequest,
  Plan,
  RegistrarPagoRequest,
  ResultadoDeImportacion,
  SituacionDelPlan,
  SuscripcionDeTenant,
  CrearUsuarioRequest,
  BusquedaSinResultado,
  Dashboard,
  FiltrosDeVehiculos,
  FiltrosDisponibles,
  FiltrosPublicos,
  GuardarConfiguracionRequest,
  GuardarVehiculoRequest,
  HealthStatus,
  HomePublica,
  LoginRequest,
  Marca,
  Modelo,
  OpcionesDeCatalogo,
  PaginaDe,
  ProblemDetails,
  RegistrarEventoRequest,
  BusquedaDeTextoRequest,
  ReporteDeDemanda,
  ResolverSolicitudRequest,
  Sesion,
  SolicitudModelo,
  SugerenciaDeCompra,
  TenantAdmin,
  TenantPublico,
  Usuario,
  VerificacionDeDominio,
  Vehiculo,
  VehiculoFoto,
  VehiculoPublico,
  VehiculoPublicoResumen,
  VehiculoResumen,
  VersionVehiculo,
} from './types'

/**
 * Base URL de la API. Sin variable definida, se asume el mismo origen que el sitio.
 *
 * Antes acá había un `throw` a nivel de módulo si la variable faltaba, y era una trampa:
 * el bundler reemplaza `import.meta.env` en tiempo de compilación, así que sin `.env` la
 * condición quedaba siempre verdadera y el minificador eliminaba la aplicación entera
 * como código inalcanzable. El build seguía diciendo "ok" y el resultado era una pantalla
 * en blanco.
 *
 * El mismo origen es además el default correcto en producción: cada automotora entra por
 * su dominio y el servidor web enruta `/api` hacia el backend.
 */
const baseUrl: string = import.meta.env.VITE_API_BASE_URL ?? ''

if (!import.meta.env.VITE_API_BASE_URL && import.meta.env.DEV) {
  console.warn(
    'VITE_API_BASE_URL no está definida: se usa el mismo origen. En desarrollo, copiá ' +
      '.env.example a .env y apuntá a http://localhost:5080.',
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
  /** Cuerpo multipart. Excluyente con `body`: el navegador arma el Content-Type. */
  form?: FormData
  signal?: AbortSignal
  /** Los endpoints de sesión no se reintentan: son los que producen el token. */
  sinReintento?: boolean
  /** Para descargas: devuelve el cuerpo como `Blob` en vez de parsear JSON. */
  comoArchivo?: boolean
}

/** Arma un query string salteando los valores vacíos, que ensuciarían la URL. */
function query(parametros: object | undefined): string {
  if (!parametros) return ''

  const partes = new URLSearchParams()

  for (const [clave, valor] of Object.entries(parametros)) {
    if (valor === undefined || valor === null || valor === '') continue
    partes.set(clave, String(valor))
  }

  const texto = partes.toString()
  return texto ? `?${texto}` : ''
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

/**
 * Si la última sesión la cerró el usuario con "Salir". En ese caso la pantalla en la que
 * estaba no se recuerda: quien entre después puede ser otra persona, con otro rol.
 */
let cerradaAPedido = false

export const sesion = {
  actual: () => sesionActual,

  fueCerradaAPedido: () => cerradaAPedido,

  suscribirse(escucha: Escucha): () => void {
    escuchas.add(escucha)
    return () => escuchas.delete(escucha)
  },

  establecer(nueva: Sesion) {
    cerradaAPedido = false
    sesionGuardada.guardar(nueva)
    publicar(nueva)
  },

  limpiar() {
    sesionGuardada.borrar()
    publicar(null)
  },

  /** El "Salir" del usuario, a diferencia de una sesión que venció. */
  cerrar() {
    cerradaAPedido = true
    sesion.limpiar()
  },
}

async function enviar(path: string, options: RequestOptions, token: string | null) {
  const { method = 'GET', body, form, signal } = options

  const headers: Record<string, string> = {}

  // Con FormData no se toca el Content-Type: el navegador tiene que ponerlo él para
  // incluir el boundary del multipart.
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  if (token) headers.Authorization = `Bearer ${token}`

  return fetch(`${baseUrl}${path}`, {
    method,
    signal,
    headers: Object.keys(headers).length === 0 ? undefined : headers,
    body: form ?? (body === undefined ? undefined : JSON.stringify(body)),
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

  if (options.comoArchivo) {
    return (await response.blob()) as TResponse
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

function formularioDeArchivo(archivo: File): FormData {
  const form = new FormData()
  form.append('archivo', archivo, archivo.name)
  return form
}

/** Dispara la descarga de un archivo que vino de la API. */
export function guardarArchivo(contenido: Blob, nombre: string) {
  const url = URL.createObjectURL(contenido)
  const enlace = document.createElement('a')
  enlace.href = url
  enlace.download = nombre
  enlace.click()
  // Se libera después: revocarlo en el acto cancela la descarga en algunos navegadores.
  setTimeout(() => URL.revokeObjectURL(url), 1000)
}

let erroresReportados = 0

/**
 * Manda a la API un error del navegador, que lo registra y llega a Sentry con el resto.
 *
 * Nunca falla ni espera: reportar un error no puede causar otro. Tope de cinco por carga
 * de página, para que un error que se repite en cada render no mande cientos. Solo el
 * camino de la URL, sin la query: la API no guarda datos de la visita.
 */
export function reportarError(error: unknown, componentes?: string) {
  if (erroresReportados >= 5) return
  erroresReportados++

  const mensaje = error instanceof Error ? `${error.name}: ${error.message}` : String(error)
  const pila = error instanceof Error ? error.stack : undefined

  fetch(`${baseUrl}/api/errores-del-cliente`, {
    method: 'POST',
    keepalive: true,
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      mensaje: mensaje.slice(0, 500),
      pila: pila?.slice(0, 4000),
      componentes: componentes?.slice(0, 2000),
      ruta: window.location.pathname.slice(0, 300),
    }),
  }).catch(() => undefined)
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

    /** Cambia la contraseña propia y devuelve una sesión nueva, ya sin la marca de provisoria. */
    cambiarPassword: (actual: string, nueva: string) =>
      request<Sesion>('/api/auth/password', { method: 'POST', body: { actual, nueva } }),

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

  catalogo: {
    marcas: (signal?: AbortSignal) => request<Marca[]>('/api/catalogo/marcas', { signal }),

    modelos: (marcaId: number, signal?: AbortSignal) =>
      request<Modelo[]>(`/api/catalogo/marcas/${marcaId}/modelos`, { signal }),

    versiones: (modeloId: number, signal?: AbortSignal) =>
      request<VersionVehiculo[]>(`/api/catalogo/modelos/${modeloId}/versiones`, { signal }),

    opciones: (signal?: AbortSignal) =>
      request<OpcionesDeCatalogo>('/api/catalogo/opciones', { signal }),

    solicitudes: (signal?: AbortSignal) =>
      request<SolicitudModelo[]>('/api/catalogo/solicitudes-modelo', { signal }),

    solicitar: (pedido: { marcaId: number; nombreModelo: string; carroceria: string }) =>
      request<SolicitudModelo>('/api/catalogo/solicitudes-modelo', { method: 'POST', body: pedido }),
  },

  vehiculos: {
    listar: (filtros: FiltrosDeVehiculos, signal?: AbortSignal) =>
      request<PaginaDe<VehiculoResumen>>(`/api/vehiculos${query(filtros)}`, { signal }),

    obtener: (id: number, signal?: AbortSignal) =>
      request<Vehiculo>(`/api/vehiculos/${id}`, { signal }),

    crear: (vehiculo: GuardarVehiculoRequest) =>
      request<Vehiculo>('/api/vehiculos', { method: 'POST', body: vehiculo }),

    actualizar: (id: number, vehiculo: GuardarVehiculoRequest) =>
      request<Vehiculo>(`/api/vehiculos/${id}`, { method: 'PUT', body: vehiculo }),

    cambiarEstado: (id: number, cambio: CambiarEstadoRequest) =>
      request<Vehiculo>(`/api/vehiculos/${id}/estado`, { method: 'POST', body: cambio }),

    borrar: (id: number) => request<void>(`/api/vehiculos/${id}`, { method: 'DELETE' }),

    plantillaDeImportacion: () =>
      request<Blob>('/api/vehiculos/importacion/plantilla', { comoArchivo: true }),

    /** Sin `confirmar`, solo valida. Con `confirmar`, carga todo o nada. */
    importar: (archivo: File, confirmar: boolean) =>
      request<ResultadoDeImportacion>(`/api/vehiculos/importacion${query({ confirmar })}`, {
        method: 'POST',
        form: formularioDeArchivo(archivo),
      }),

    fotos: {
      subir: (vehiculoId: number, imagen: Blob, nombre: string) => {
        const form = new FormData()
        form.append('imagen', imagen, nombre)

        return request<VehiculoFoto>(`/api/vehiculos/${vehiculoId}/fotos`, { method: 'POST', form })
      },

      borrar: (vehiculoId: number, fotoId: number) =>
        request<void>(`/api/vehiculos/${vehiculoId}/fotos/${fotoId}`, { method: 'DELETE' }),

      reordenar: (vehiculoId: number, fotoIds: number[]) =>
        request<VehiculoFoto[]>(`/api/vehiculos/${vehiculoId}/fotos/orden`, {
          method: 'PUT',
          body: { fotoIds },
        }),

      portada: (vehiculoId: number, fotoId: number) =>
        request<VehiculoFoto[]>(`/api/vehiculos/${vehiculoId}/fotos/${fotoId}/portada`, {
          method: 'POST',
        }),
    },
  },

  tenant: {
    obtener: (signal?: AbortSignal) =>
      request<ConfiguracionDeTenant>('/api/tenant', { signal }),

    guardar: (configuracion: GuardarConfiguracionRequest) =>
      request<ConfiguracionDeTenant>('/api/tenant', { method: 'PUT', body: configuracion }),

    logo: (imagen: Blob, nombre: string) => {
      const form = new FormData()
      form.append('imagen', imagen, nombre)

      return request<ConfiguracionDeTenant>('/api/tenant/logo', { method: 'POST', form })
    },

    /** El plan, el uso contra los topes y el estado del pago. Alimenta los avisos del panel. */
    plan: (signal?: AbortSignal) => request<SituacionDelPlan>('/api/tenant/plan', { signal }),

    /** ZIP con todos los datos de la automotora. */
    exportar: () => request<Blob>('/api/tenant/exportacion', { comoArchivo: true }),
  },

  dashboard: (signal?: AbortSignal) => request<Dashboard>('/api/dashboard', { signal }),

  /**
   * Los reportes de demanda. La ventana viaja en días y la acota el servidor: el cliente
   * pide lo que quiere y el que decide cuánto se puede consultar es el que paga la
   * consulta.
   */
  reportes: {
    demanda: (dias: number, signal?: AbortSignal) =>
      request<ReporteDeDemanda>(`/api/reportes/demanda${query({ dias })}`, { signal }),

    busquedasSinResultado: (dias: number, signal?: AbortSignal) =>
      request<BusquedaSinResultado[]>(`/api/reportes/busquedas-sin-resultado${query({ dias })}`, {
        signal,
      }),

    sugerencias: (dias: number, signal?: AbortSignal) =>
      request<SugerenciaDeCompra[]>(`/api/reportes/sugerencias${query({ dias })}`, { signal }),

    benchmark: (dias: number, signal?: AbortSignal) =>
      request<Benchmark>(`/api/reportes/benchmark${query({ dias })}`, { signal }),
  },

  admin: {
    tenants: (signal?: AbortSignal) => request<TenantAdmin[]>('/api/admin/tenants', { signal }),

    crearTenant: (nuevo: CrearTenantRequest) =>
      request<TenantAdmin>('/api/admin/tenants', { method: 'POST', body: nuevo }),

    /**
     * Comprueba que el dominio propio apunte a la aplicación y lo habilita si es así.
     *
     * Se dispara a mano: una consulta de DNS por visita sería una llamada saliente en el
     * camino caliente del sitio, y el dato cambia una vez en la vida del dominio.
     */
    verificarDominio: (id: number) =>
      request<VerificacionDeDominio>(`/api/admin/tenants/${id}/verificar-dominio`, { method: 'POST' }),

    /** Contraseña provisoria para un usuario de la automotora: el camino del dueño que se olvidó la suya. */
    restablecerPassword: (id: number, email: string, password: string) =>
      request<void>(`/api/admin/tenants/${id}/restablecer-password`, {
        method: 'POST',
        body: { email, password },
      }),

    actualizarTenant: (id: number, cambios: ActualizarTenantRequest) =>
      request<TenantAdmin>(`/api/admin/tenants/${id}`, { method: 'PUT', body: cambios }),

    planes: (signal?: AbortSignal) => request<Plan[]>('/api/admin/planes', { signal }),

    crearPlan: (plan: GuardarPlanRequest) =>
      request<Plan>('/api/admin/planes', { method: 'POST', body: plan }),

    actualizarPlan: (id: number, plan: GuardarPlanRequest) =>
      request<Plan>(`/api/admin/planes/${id}`, { method: 'PUT', body: plan }),

    /** Todas las automotoras, las más urgentes primero. */
    cobranza: (signal?: AbortSignal) => request<FilaDeCobranza[]>('/api/admin/cobranza', { signal }),

    suscripcion: (tenantId: number, signal?: AbortSignal) =>
      request<SuscripcionDeTenant>(`/api/admin/tenants/${tenantId}/suscripcion`, { signal }),

    cambiarPlan: (tenantId: number, plan: string) =>
      request<SuscripcionDeTenant>(`/api/admin/tenants/${tenantId}/suscripcion`, {
        method: 'POST',
        body: { plan },
      }),

    registrarPago: (tenantId: number, pago: RegistrarPagoRequest) =>
      request<SuscripcionDeTenant>(`/api/admin/tenants/${tenantId}/pagos`, { method: 'POST', body: pago }),

    darDeBaja: (tenantId: number, fecha: string, motivo: string) =>
      request<SuscripcionDeTenant>(`/api/admin/tenants/${tenantId}/baja`, {
        method: 'POST',
        body: { fecha, motivo },
      }),

    importarStock: (tenantId: number, archivo: File, confirmar: boolean) =>
      request<ResultadoDeImportacion>(`/api/admin/tenants/${tenantId}/importacion${query({ confirmar })}`, {
        method: 'POST',
        form: formularioDeArchivo(archivo),
      }),

    carrocerias: (signal?: AbortSignal) =>
      request<string[]>('/api/admin/catalogo/carrocerias', { signal }),

    marcas: (signal?: AbortSignal) => request<Marca[]>('/api/admin/catalogo/marcas', { signal }),

    crearMarca: (marca: { nombre: string; activo: boolean }) =>
      request<Marca>('/api/admin/catalogo/marcas', { method: 'POST', body: marca }),

    actualizarMarca: (id: number, marca: { nombre: string; activo: boolean }) =>
      request<Marca>(`/api/admin/catalogo/marcas/${id}`, { method: 'PUT', body: marca }),

    modelos: (marcaId: number | undefined, signal?: AbortSignal) =>
      request<Modelo[]>(`/api/admin/catalogo/modelos${query({ marcaId })}`, { signal }),

    crearModelo: (modelo: { marcaId: number; nombre: string; carroceria: string; activo: boolean }) =>
      request<Modelo>('/api/admin/catalogo/modelos', { method: 'POST', body: modelo }),

    actualizarModelo: (
      id: number,
      modelo: { marcaId: number; nombre: string; carroceria: string; activo: boolean },
    ) => request<Modelo>(`/api/admin/catalogo/modelos/${id}`, { method: 'PUT', body: modelo }),

    versiones: (modeloId: number | undefined, signal?: AbortSignal) =>
      request<VersionVehiculo[]>(`/api/admin/catalogo/versiones${query({ modeloId })}`, { signal }),

    crearVersion: (version: { modeloId: number; nombre: string; activo: boolean }) =>
      request<VersionVehiculo>('/api/admin/catalogo/versiones', { method: 'POST', body: version }),

    solicitudes: (estado: string | undefined, signal?: AbortSignal) =>
      request<SolicitudModelo[]>(`/api/admin/solicitudes-modelo${query({ estado })}`, { signal }),

    resolverSolicitud: (id: number, resolucion: ResolverSolicitudRequest) =>
      request<SolicitudModelo>(`/api/admin/solicitudes-modelo/${id}/resolver`, {
        method: 'POST',
        body: resolucion,
      }),
  },

  publico: {
    tenant: (slug: string | null, signal?: AbortSignal) =>
      request<TenantPublico>(rutaPublica(slug, '/api/public/tenant'), { signal }),

    home: (slug: string | null, signal?: AbortSignal) =>
      request<HomePublica>(rutaPublica(slug, '/api/public/home'), { signal }),

    filtros: (slug: string | null, signal?: AbortSignal) =>
      request<FiltrosDisponibles>(rutaPublica(slug, '/api/public/filtros'), { signal }),

    vehiculos: (slug: string | null, filtros: FiltrosPublicos, signal?: AbortSignal) =>
      request<PaginaDe<VehiculoPublicoResumen>>(
        rutaPublica(slug, `/api/public/vehiculos${query(filtros)}`),
        { signal },
      ),

    vehiculo: (slug: string | null, id: number, signal?: AbortSignal) =>
      request<VehiculoPublico>(rutaPublica(slug, `/api/public/vehiculos/${id}`), { signal }),

    /**
     * Registra un evento. No se espera ni se propaga el error: si la métrica no se pudo
     * guardar, el visitante no tiene por qué enterarse ni por qué frenar su navegación.
     */
    evento: (slug: string | null, evento: RegistrarEventoRequest) =>
      request<void>(rutaPublica(slug, '/api/public/events'), {
        method: 'POST',
        body: evento,
        sinReintento: true,
      }).catch(() => undefined),

    /** Registra lo que se buscó en la portada y no se encontró. Como los eventos, sin propagar errores. */
    busqueda: (slug: string | null, busqueda: BusquedaDeTextoRequest) =>
      request<void>(rutaPublica(slug, '/api/public/busquedas'), {
        method: 'POST',
        body: busqueda,
        sinReintento: true,
      }).catch(() => undefined),
  },
}
