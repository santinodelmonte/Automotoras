import { Navigate } from 'react-router-dom'
import { useSesion } from '@shared/auth/useSesion'
import { DashboardPage } from '@admin/paginas/DashboardPage'

/**
 * Adónde cae cada rol al entrar al panel.
 *
 * El tablero es del dueño; el vendedor arranca en el stock, que es su trabajo; el
 * SuperAdmin, en la lista de automotoras. Mandar a todos al mismo lugar significaría que
 * dos de los tres roles ven una pantalla en la que no pueden hacer nada — y en el caso del
 * vendedor, una que el servidor le responde 403.
 */
export function InicioDelPanel() {
  const sesion = useSesion()

  if (!sesion) return null

  switch (sesion.usuario.rol) {
    case 'SuperAdmin':
      return <Navigate to="/admin/automotoras" replace />
    case 'Seller':
      return <Navigate to="/admin/vehiculos" replace />
    default:
      return <DashboardPage />
  }
}
