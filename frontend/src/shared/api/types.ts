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

/** Identidad pública de la automotora: lo que el sitio necesita para pintarse. */
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

export interface CrearUsuarioRequest {
  email: string
  nombre: string
  password: string
  rol: Rol
}
