import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { sesion as almacenDeSesion } from '@shared/api/client'
import { useSesion } from '@shared/auth/useSesion'
import type { Rol } from '@shared/api/types'

interface Props {
  /** Roles habilitados. Sin lista, alcanza con estar autenticado. */
  roles?: Rol[]
  children: ReactNode
}

/**
 * Corta el paso en el cliente. No es la seguridad: la seguridad está en el servidor, que
 * responde 401 y 403 aunque el navegador pinte lo que sea. Esto es para que nadie llegue a
 * una pantalla vacía llena de errores.
 */
export function RutaProtegida({ roles, children }: Props) {
  const sesion = useSesion()
  const ubicacion = useLocation()

  if (!sesion) {
    // Si la sesión venció, al volver a entrar se retoma donde estaba. Si apretó "Salir",
    // no: el próximo en entrar puede ser otra persona.
    const estado = almacenDeSesion.fueCerradaAPedido() ? undefined : { desde: ubicacion.pathname }
    return <Navigate to="/admin/login" state={estado} replace />
  }

  if (roles && !roles.includes(sesion.usuario.rol)) {
    return <Navigate to="/admin" replace />
  }

  return children
}
