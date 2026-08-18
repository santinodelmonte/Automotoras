import { useSyncExternalStore } from 'react'
import { sesion } from '@shared/api/client'
import type { Rol, Sesion } from '@shared/api/types'

/**
 * La sesión en curso, o `null`. Se lee del store del cliente de API, que es el mismo que
 * el interceptor actualiza al renovar el token: así una renovación en segundo plano
 * repinta la UI sin que nadie tenga que acordarse de avisarle.
 */
export function useSesion(): Sesion | null {
  return useSyncExternalStore(sesion.suscribirse, sesion.actual, sesion.actual)
}

export function useTieneRol(...roles: Rol[]): boolean {
  const actual = useSesion()

  return actual !== null && roles.includes(actual.usuario.rol)
}
