/** Respuesta de `GET /api/health`. */
export interface HealthStatus {
  status: string
  timestamp: string
}

/** Error de la API en formato ProblemDetails (RFC 7807). */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  errors?: Record<string, string[]>
}

/** Una página de resultados, con el total para pintar el paginador. */
export interface PaginaDe<T> {
  items: T[]
  total: number
  pagina: number
  porPagina: number
  totalDePaginas: number
}

// ---------------------------------------------------------------- identidad

/** Roles del sistema. Coinciden con el claim `role` del JWT. */
export type Rol = 'SuperAdmin' | 'Owner' | 'Seller'

/** Usuario tal como lo devuelve la API. Nunca lleva la contraseña ni su hash. */
export interface Usuario {
  id: number
  /** Nulo en el SuperAdmin, que no pertenece a ninguna automotora. */
  tenantId: number | null
  email: string
  nombre: string
  rol: Rol
  activo: boolean
}

/** Sesión abierta: el par de tokens y a quién pertenecen. */
export interface Sesion {
  accessToken: string
  expiraEn: string
  refreshToken: string
  usuario: Usuario
}

export interface LoginRequest {
  email: string
  password: string
}

export interface CrearUsuarioRequest {
  email: string
  nombre: string
  password: string
  rol: Rol
}

// ---------------------------------------------------------------- catálogo

export interface Marca {
  id: number
  nombre: string
  activo: boolean
}

export interface Modelo {
  id: number
  marcaId: number
  nombre: string
  carroceria: string
  activo: boolean
}

export interface VersionVehiculo {
  id: number
  modeloId: number
  nombre: string
  activo: boolean
}

/**
 * Las opciones fijas de los formularios, servidas por el servidor.
 *
 * Duplicar los enums acá garantizaría que algún día un select ofrezca un valor que la API
 * rechaza.
 */
export interface OpcionesDeCatalogo {
  carrocerias: string[]
  combustibles: string[]
  transmisiones: string[]
  monedas: string[]
  estadosDeVehiculo: string[]
}

export type EstadoSolicitud = 'Pendiente' | 'Aprobada' | 'Rechazada'

export interface SolicitudModelo {
  id: number
  marcaId: number
  marca: string
  nombreModelo: string
  carroceria: string
  estado: EstadoSolicitud
  solicitadaPor: string
  createdAt: string
  resueltaEn: string | null
  notaResolucion: string | null
  modeloCreadoId: number | null
}

// ---------------------------------------------------------------- vehículos

export type EstadoVehiculo = 'Disponible' | 'Reservado' | 'Vendido' | 'Pausado'

export interface VehiculoFoto {
  id: number
  url: string
  urlThumb: string | null
  orden: number
  esPortada: boolean
}

export interface Vehiculo {
  id: number
  marcaId: number
  marca: string
  modeloId: number
  modelo: string
  versionId: number | null
  version: string | null
  carroceria: string
  anio: number
  kilometraje: number
  combustible: string
  transmision: string
  color: string | null
  puertas: number | null
  motor: string | null
  precio: number
  moneda: string
  estado: EstadoVehiculo
  descripcion: string | null
  destacado: boolean
  /** Nulo cuando quien pregunta es un Seller: el dato no sale del servidor. */
  precioCosto: number | null
  fechaPublicacion: string
  fechaVenta: string | null
  precioVenta: number | null
  diasEnGondola: number
  fotos: VehiculoFoto[]
  createdAt: string
  updatedAt: string
}

export interface VehiculoResumen {
  id: number
  marca: string
  modelo: string
  version: string | null
  anio: number
  kilometraje: number
  precio: number
  moneda: string
  estado: EstadoVehiculo
  destacado: boolean
  fotoPortadaUrl: string | null
  diasEnGondola: number
  fechaPublicacion: string
}

export interface GuardarVehiculoRequest {
  modeloId: number
  versionId: number | null
  anio: number
  kilometraje: number
  combustible: string
  transmision: string
  color: string | null
  puertas: number | null
  motor: string | null
  precio: number
  moneda: string
  descripcion: string | null
  destacado: boolean
  precioCosto: number | null
  fechaPublicacion: string | null
}

export interface CambiarEstadoRequest {
  estado: EstadoVehiculo
  fechaVenta: string | null
  precioVenta: number | null
}

export interface FiltrosDeVehiculos {
  estado?: EstadoVehiculo | ''
  marcaId?: number
  modeloId?: number
  texto?: string
  pagina?: number
  porPagina?: number
}

// ---------------------------------------------------------------- público

export interface TenantPublico {
  slug: string
  nombre: string
  logoUrl: string | null
  colorPrimario: string | null
  colorSecundario: string | null
  whatsapp: string | null
  telefono: string | null
  direccion: string | null
}

export interface VehiculoPublicoResumen {
  id: number
  marca: string
  modelo: string
  version: string | null
  carroceria: string
  anio: number
  kilometraje: number
  precio: number
  moneda: string
  combustible: string
  transmision: string
  fotoPortadaUrl: string | null
  destacado: boolean
}

export interface VehiculoPublico {
  id: number
  marca: string
  modelo: string
  version: string | null
  carroceria: string
  anio: number
  kilometraje: number
  combustible: string
  transmision: string
  color: string | null
  puertas: number | null
  motor: string | null
  precio: number
  moneda: string
  descripcion: string | null
  destacado: boolean
  fotos: VehiculoFoto[]
  titulo: string
  mensajeDeWhatsapp: string
}

export interface ModeloConStock {
  id: number
  nombre: string
}

export interface MarcaConStock {
  id: number
  nombre: string
  modelos: ModeloConStock[]
}

/**
 * Lo que se puede filtrar en este sitio ahora mismo: no el catálogo global, sino lo que
 * esta automotora tiene publicado. Un filtro que siempre devuelve cero le hace perder el
 * tiempo al comprador.
 */
export interface FiltrosDisponibles {
  marcas: MarcaConStock[]
  carrocerias: string[]
  combustibles: string[]
  transmisiones: string[]
  monedas: string[]
  anioMinimo: number | null
  anioMaximo: number | null
}

export interface HomePublica {
  destacados: VehiculoPublicoResumen[]
  recientes: VehiculoPublicoResumen[]
  totalDisponibles: number
}

export interface FiltrosPublicos {
  marcaId?: number
  modeloId?: number
  anioDesde?: number
  anioHasta?: number
  moneda?: string
  precioDesde?: number
  precioHasta?: number
  kmDesde?: number
  kmHasta?: number
  combustible?: string
  transmision?: string
  carroceria?: string
  orden?: string
  pagina?: number
  porPagina?: number
  sessionId?: string
}

export type TipoEvento =
  | 'ViewFicha'
  | 'ViewListado'
  | 'ClickWhatsapp'
  | 'ClickTelefono'
  | 'BusquedaSinResultado'

export interface RegistrarEventoRequest {
  tipo: TipoEvento
  vehiculoId: number | null
  sessionId: string | null
}

// ---------------------------------------------------------------- panel

export interface ConfiguracionDeTenant {
  slug: string
  nombre: string
  dominioCustom: string | null
  logoUrl: string | null
  colorPrimario: string | null
  colorSecundario: string | null
  whatsapp: string | null
  telefono: string | null
  direccion: string | null
}

export interface GuardarConfiguracionRequest {
  nombre: string
  colorPrimario: string | null
  colorSecundario: string | null
  whatsapp: string | null
  telefono: string | null
  direccion: string | null
}

export interface ConteoPorEstado {
  estado: EstadoVehiculo
  cantidad: number
}

export interface VehiculoMasVisto {
  vehiculoId: number
  marca: string
  modelo: string
  anio: number
  fotoPortadaUrl: string | null
  vistas: number
  consultas: number
}

export interface Dashboard {
  vehiculosPorEstado: ConteoPorEstado[]
  totalDeVehiculos: number
  vistasUltimos30Dias: number
  consultasUltimos30Dias: number
  busquedasSinResultadoUltimos30Dias: number
  diasEnGondolaPromedio: number
  masVistos: VehiculoMasVisto[]
}

// ---------------------------------------------------------------- reportes

/**
 * Lo que el comportamiento de los compradores dice sobre una unidad.
 *
 * Es una señal, no un veredicto: el reporte sugiere dónde mirar, la decisión la toma quien
 * conoce el negocio.
 */
export type SenalDeDemanda = 'PocosDatos' | 'Normal' | 'PrecioAlto' | 'SinInteres'

export interface VehiculoEnGondola {
  vehiculoId: number
  marca: string
  modelo: string
  version: string | null
  anio: number
  precio: number
  moneda: string
  estado: EstadoVehiculo
  fotoPortadaUrl: string | null
  diasEnGondola: number
  vistas: number
  consultas: number
  consultasPorCienVistas: number
  senal: SenalDeDemanda
  /** La señal explicada en una frase, para no dejar el número solo. */
  lectura: string
  /**
   * Promedio relevado para ese modelo y año, o `null` si todavía no se relevó. Nulo es
   * "no sabemos", nunca "vale cero".
   */
  precioDeMercado: number | null
  /** Porcentaje por encima (positivo) o por debajo (negativo) del promedio de mercado. */
  diferenciaConElMercado: number | null
}

export interface DemandaInsatisfecha {
  marca: string | null
  modelo: string | null
  carroceria: string | null
  combustible: string | null
  transmision: string | null
  anioDesde: number | null
  precioHasta: number | null
  moneda: string | null
  veces: number
  ultimaVez: string
  descripcion: string
}

/**
 * Qué conviene traer, y por qué.
 *
 * `unidadesVendidasSimilares` en `null` no es cero: es que la automotora todavía no vendió
 * suficientes unidades parecidas como para hablar de rotación.
 */
export interface SugerenciaDeCompra {
  descripcion: string
  fundamento: string
  busquedasSinResultado: number
  ultimaBusqueda: string
  marca: string | null
  modelo: string | null
  carroceria: string | null
  anioDesde: number | null
  precioHasta: number | null
  moneda: string | null
  unidadesVendidasSimilares: number | null
  diasPromedioParaVender: number | null
}

/**
 * Un indicador propio contra el mismo indicador del resto del mercado.
 *
 * `propio` en `null` no es cero: es que esta automotora todavía no tiene datos para
 * calcularlo. `automotorasAportantes` dice cuánto pesa el número del mercado, y nunca
 * incluye a la propia.
 */
export interface Comparativo {
  dimension: string
  propio: number | null
  mercado: number
  automotorasAportantes: number
  registrosAportantes: number
  /** La comparación explicada en una frase, para no dejar los dos números solos. */
  lectura: string
}

/**
 * Cómo le va a esta automotora contra el resto, sin que ninguna otra sea identificable.
 *
 * Las comparaciones que no llegan al mínimo de automotoras detrás no vienen recortadas ni
 * en cero: no vienen. Una lista corta significa poco mercado relevado, no un mal resultado.
 */
export interface Benchmark {
  diasAnalizados: number
  diasParaVenderPorCarroceria: Comparativo[]
  consultasPorCienVistas: Comparativo | null
  notaDePrivacidad: string
}

export interface ReporteDeDemanda {
  diasAnalizados: number
  vistasTotales: number
  consultasTotales: number
  vehiculos: VehiculoEnGondola[]
  demandaInsatisfecha: DemandaInsatisfecha[]
}

// ---------------------------------------------------------------- superadmin

export interface TenantAdmin {
  id: number
  slug: string
  nombre: string
  dominioCustom: string | null
  logoUrl: string | null
  colorPrimario: string | null
  colorSecundario: string | null
  whatsapp: string | null
  telefono: string | null
  direccion: string | null
  activo: boolean
  createdAt: string
  usuarios: number
  vehiculos: number
}

export interface CrearTenantRequest {
  slug: string
  nombre: string
  dominioCustom: string | null
  emailDelOwner: string
  nombreDelOwner: string
  passwordDelOwner: string
}

export interface ActualizarTenantRequest {
  slug: string
  nombre: string
  dominioCustom: string | null
  activo: boolean
}

export interface ResolverSolicitudRequest {
  aprobar: boolean
  nota: string | null
}
