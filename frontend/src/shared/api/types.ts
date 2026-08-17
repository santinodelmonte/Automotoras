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
