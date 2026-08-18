import { createContext, useContext } from 'react'
import type { TenantPublico } from '@shared/api/types'

interface ContextoDeSitio {
  tenant: TenantPublico
  /** Slug de la ruta en desarrollo, o `null` cuando se entró por el dominio propio. */
  slug: string | null
}

export const TenantContexto = createContext<ContextoDeSitio | null>(null)

/**
 * La automotora del sitio. Solo se puede usar dentro del layout público, que es el que la
 * resolvió: si no hay automotora, no hay sitio que pintar.
 */
export function useSitio(): ContextoDeSitio {
  const contexto = useContext(TenantContexto)

  if (!contexto) {
    throw new Error('useSitio() se usó fuera del sitio público.')
  }

  return contexto
}

/** Color de marca de la automotora, con una caída razonable si no configuró ninguno. */
export function colorPrimario(tenant: TenantPublico): string {
  return tenant.colorPrimario ?? '#0f172a'
}
