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
  /** Límite del plan (`type: 'limite-del-plan'`): qué recurso, el tope y el uso actual. */
  recurso?: string
  plan?: string | null
  tope?: number | null
  uso?: number | null
  /** Sitio en mantenimiento (`type: 'sitio-en-mantenimiento'`): de qué automotora. */
  automotora?: string
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
  /** La contraseña la puso otra persona: hay que cambiarla antes de usar el panel. */
  debeCambiarPassword?: boolean
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

// ------------------------------------------------------------------ reportes

/**
 * La lectura de una unidad en una palabra, tal como la calcula el servidor.
 *
 * Se decide en el backend y no acá: es una regla de negocio con umbrales, y duplicarla en
 * el cliente sería garantizar que un día el panel y la API digan cosas distintas del
 * mismo vehículo.
 */
export type SenalDeDemanda =
  | 'SinDatos'
  | 'Saludable'
  | 'PrecioAlto'
  | 'SinVisibilidad'
  | 'Estancado'

export interface DemandaDeVehiculo {
  vehiculoId: number
  marca: string
  modelo: string
  anio: number
  estado: EstadoVehiculo
  precio: number
  moneda: string
  fotoPortadaUrl: string | null
  diasEnGondola: number
  vistas: number
  consultas: number
  consultasPorCienVistas: number
  senal: SenalDeDemanda
  /** Mediana de lo que se pide en el mercado por ese modelo y año. `null` hasta que el job de precios corra. */
  precioDeMercado: number | null
  /** Porcentaje por encima (positivo) o por debajo del mercado. */
  diferenciaConElMercado: number | null
  /** Día del snapshot: un precio de referencia sin fecha se compara como si fuera de hoy. */
  precioDeMercadoAl: string | null
}

export interface ResumenDeDemanda {
  dias: number
  vehiculosPublicados: number
  vistas: number
  consultas: number
  consultasPorCienVistas: number
  busquedasSinResultado: number
  diasEnGondolaPromedio: number
  diasEnGondolaMediana: number
  vendidosEnElPeriodo: number
  diasHastaLaVentaPromedio: number | null
}

export interface ReporteDeDemanda {
  resumen: ResumenDeDemanda
  vehiculos: DemandaDeVehiculo[]
}

export interface BusquedaSinResultado {
  marcaId: number | null
  marca: string | null
  modeloId: number | null
  modelo: string | null
  carroceria: string | null
  anioDesde: number | null
  anioHasta: number | null
  moneda: string | null
  precioDesde: number | null
  precioHasta: number | null
  presupuestoTipico: number | null
  veces: number
  sesiones: number
  ultimaVez: string
}

/** `Comprar` cuando no hay una sola unidad de eso; si hay, el problema es otro. */
export type TipoDeSugerencia = 'Comprar' | 'RevisarLoQueTenes'

export interface SugerenciaDeCompra {
  tipo: TipoDeSugerencia
  marcaId: number | null
  marca: string | null
  modeloId: number | null
  modelo: string | null
  carroceria: string | null
  anioDesde: number | null
  anioHasta: number | null
  moneda: string | null
  presupuestoTipico: number | null
  visitas: number
  busquedas: number
  ultimaVez: string
  unidadesEnStock: number
}

export interface MetricaComparada {
  propio: number | null
  /** Mediana entre automotoras. Nunca un extremo: un extremo es el dato de una sola con otro nombre. */
  mercado: number | null
  mejorCuandoBaja: boolean
}

export interface Benchmark {
  dias: number
  /** `false` mientras la muestra sea chica: no se publica un agregado que delate al vecino. */
  disponible: boolean
  motivo: string | null
  automotorasEnLaMuestra: number
  diasEnGondola: MetricaComparada | null
  consultasPorCienVistas: MetricaComparada | null
  diasHastaLaVenta: MetricaComparada | null
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
  /** Cuándo se comprobó que el dominio apunta acá. `null` mientras no se verificó: hasta entonces no sirve el sitio. */
  dominioVerificadoEn: string | null
}

export interface VerificacionDeDominio {
  resultado: 'Verificado' | 'NoResuelve' | 'ApuntaAOtroLado' | 'SinDominio' | 'SinIpsDeclaradas'
  detalle: string
  verificadoEn: string | null
  /** A dónde resuelve hoy. Viaja sobre todo cuando falla: sin esto no hay con qué comparar lo que se cargó. */
  apuntaA: string[]
  deberiaApuntarA: string[]
}

export interface CrearTenantRequest {
  slug: string
  nombre: string
  dominioCustom: string | null
  emailDelOwner: string
  nombreDelOwner: string
  passwordDelOwner: string
  /** Código del plan. Sin él, el servidor asigna el plan por defecto. */
  plan?: string | null
  colorPrimario?: string | null
  colorSecundario?: string | null
  whatsapp?: string | null
  telefono?: string | null
  direccion?: string | null
}

export interface ActualizarTenantRequest {
  slug: string
  nombre: string
  dominioCustom: string | null
  activo: boolean
}

// ---------------------------------------------------------------- Planes y cobranza

export type EstadoDeCobro = 'Vigente' | 'PorVencer' | 'Gracia' | 'Suspendido'

export interface Plan {
  id: number
  codigo: string
  nombre: string
  precioMensual: number
  moneda: string
  /** Nulo es sin límite. */
  maxVehiculos: number | null
  maxUsuarios: number | null
  incluyeReportes: boolean
  incluyeBenchmark: boolean
  incluyeDominioPropio: boolean
  horasSoporteMes: number
  activo: boolean
}

export type GuardarPlanRequest = Omit<Plan, 'id'>

export interface Uso {
  usados: number
  tope: number | null
  /** Pasó el 80 % del tope. */
  cercaDelTope: boolean
}

export interface SituacionDelPlan {
  plan: Plan | null
  estado: EstadoDeCobro
  pagaHasta: string | null
  /** Cero es "vence hoy"; negativo, días vencido. */
  diasParaVencer: number | null
  vehiculos: Uso
  usuarios: Uso
}

export interface Suscripcion {
  id: number
  plan: Plan
  inicio: string
  fin: string | null
  pagaHasta: string
  motivoDeBaja: string | null
}

export interface Pago {
  id: number
  suscripcionId: number
  fecha: string
  monto: number
  moneda: string
  periodoDesde: string
  periodoHasta: string
  medio: string
  comprobante: string | null
  nota: string | null
}

export interface SuscripcionDeTenant {
  tenantId: number
  nombre: string
  situacion: SituacionDelPlan
  vigente: Suscripcion | null
  historial: Suscripcion[]
  pagos: Pago[]
}

export interface FilaDeCobranza {
  tenantId: number
  nombre: string
  slug: string
  activo: boolean
  plan: string | null
  pagaHasta: string | null
  diasParaVencer: number | null
  estado: EstadoDeCobro
  vehiculos: Uso
  usuarios: Uso
}

export interface RegistrarPagoRequest {
  fecha: string
  monto: number
  /** Sin él, cubre desde el día siguiente al último pago. */
  periodoDesde: string | null
  periodoHasta: string
  medio: string
  moneda?: string | null
  comprobante?: string | null
  nota?: string | null
}

export interface ErrorDeImportacion {
  /** Línea del archivo, contando el encabezado. Cero es un error del archivo entero. */
  fila: number
  columna: string | null
  mensaje: string
}

export interface ResultadoDeImportacion {
  filas: number
  validas: number
  errores: ErrorDeImportacion[]
  importados: number
}

export interface ResolverSolicitudRequest {
  aprobar: boolean
  nota: string | null
}
