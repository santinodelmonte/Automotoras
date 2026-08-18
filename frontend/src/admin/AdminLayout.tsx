import { NavLink, Outlet } from 'react-router-dom'
import { api, sesion } from '@shared/api/client'
import { useSesion } from '@shared/auth/useSesion'

/** Navegación del panel, recortada por rol. */
const enlaces = [
  { a: '/admin', texto: 'Tablero', roles: ['Owner'], exacto: true },
  { a: '/admin/vehiculos', texto: 'Vehículos', roles: ['Owner', 'Seller'], exacto: false },
  { a: '/admin/usuarios', texto: 'Usuarios', roles: ['Owner'], exacto: false },
  { a: '/admin/configuracion', texto: 'Configuración', roles: ['Owner'], exacto: false },
  { a: '/admin/automotoras', texto: 'Automotoras', roles: ['SuperAdmin'], exacto: false },
  { a: '/admin/catalogo', texto: 'Catálogo', roles: ['SuperAdmin'], exacto: false },
  { a: '/admin/solicitudes', texto: 'Solicitudes', roles: ['SuperAdmin'], exacto: false },
]

export function AdminLayout() {
  const actual = useSesion()

  if (!actual) return null

  const { usuario } = actual
  const visibles = enlaces.filter((e) => e.roles.includes(usuario.rol))

  async function salir() {
    const abierta = sesion.actual()

    if (abierta) {
      // Se avisa al servidor para que revoque el refresh token. Si el llamado falla, la
      // sesión local se cierra igual: dejar al usuario adentro porque no hubo red sería
      // lo peor de los dos mundos.
      try {
        await api.auth.logout(abierta.refreshToken)
      } catch {
        /* el token vence solo */
      }
    }

    sesion.limpiar()
  }

  return (
    <div className="min-h-screen bg-slate-100 text-slate-900">
      <header className="border-b border-slate-200 bg-white">
        <div className="mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-3 px-4 py-3">
          <div className="flex items-center gap-6">
            <span className="text-sm font-bold uppercase tracking-widest text-emerald-600">
              Automotora
            </span>

            <nav className="flex flex-wrap gap-4 text-sm font-medium">
              {visibles.map((enlace) => (
                <NavLink
                  key={enlace.a}
                  to={enlace.a}
                  end={enlace.exacto}
                  className={({ isActive }) =>
                    isActive ? 'text-emerald-700 underline' : 'text-slate-600 hover:text-slate-900'
                  }
                >
                  {enlace.texto}
                </NavLink>
              ))}
            </nav>
          </div>

          <div className="flex items-center gap-3 text-sm">
            <span className="text-slate-500">
              {usuario.nombre} · {usuario.rol}
            </span>
            <button
              type="button"
              onClick={() => void salir()}
              className="rounded-lg border border-slate-300 px-3 py-1.5 hover:border-slate-500"
            >
              Salir
            </button>
          </div>
        </div>
      </header>

      <main className="mx-auto w-full max-w-6xl px-4 py-6">
        <Outlet />
      </main>
    </div>
  )
}
