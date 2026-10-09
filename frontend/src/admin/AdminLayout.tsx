import { useState } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router-dom'
import { Dialog } from 'radix-ui'
import {
  BookOpen,
  Car,
  CreditCard,
  Inbox,
  LayoutDashboard,
  LogOut,
  Menu,
  Receipt,
  Settings,
  Store,
  TrendingUp,
  Users,
  X,
  type LucideIcon,
} from 'lucide-react'
import { api, sesion } from '@shared/api/client'
import { useSesion } from '@shared/auth/useSesion'
import type { Rol } from '@shared/api/types'
import { AvisoDelPlan } from '@admin/AvisoDelPlan'
import { CambioDePasswordObligatorio } from '@admin/CambioDePasswordObligatorio'
import { Marca } from '@admin/ui/Marca'

interface Enlace {
  a: string
  texto: string
  icono: LucideIcon
  roles: Rol[]
  exacto?: boolean
}

/** Navegación del panel, recortada por rol. */
const enlaces: Enlace[] = [
  { a: '/admin', texto: 'Tablero', icono: LayoutDashboard, roles: ['Owner'], exacto: true },
  { a: '/admin/vehiculos', texto: 'Vehículos', icono: Car, roles: ['Owner', 'Seller'] },
  { a: '/admin/reportes', texto: 'Demanda', icono: TrendingUp, roles: ['Owner'] },
  { a: '/admin/usuarios', texto: 'Usuarios', icono: Users, roles: ['Owner'] },
  { a: '/admin/configuracion', texto: 'Configuración', icono: Settings, roles: ['Owner'] },
  { a: '/admin/plan', texto: 'Mi plan', icono: CreditCard, roles: ['Owner'] },
  { a: '/admin/automotoras', texto: 'Automotoras', icono: Store, roles: ['SuperAdmin'] },
  { a: '/admin/cobranza', texto: 'Cobranza', icono: Receipt, roles: ['SuperAdmin'] },
  { a: '/admin/catalogo', texto: 'Catálogo', icono: BookOpen, roles: ['SuperAdmin'] },
  { a: '/admin/solicitudes', texto: 'Solicitudes', icono: Inbox, roles: ['SuperAdmin'] },
]

const ROLES: Record<Rol, string> = {
  Owner: 'Dueño',
  Seller: 'Vendedor',
  SuperAdmin: 'Administrador',
}

export function AdminLayout() {
  const actual = useSesion()
  const ubicacion = useLocation()
  const [menuAbierto, setMenuAbierto] = useState(false)
  const [rutaDelMenu, setRutaDelMenu] = useState(ubicacion.pathname)

  // El menú del celular se cierra solo al navegar: tocar un enlace y quedarse mirando
  // el menú tapando la pantalla nueva obliga a un toque más que nadie entiende.
  if (rutaDelMenu !== ubicacion.pathname) {
    setRutaDelMenu(ubicacion.pathname)
    setMenuAbierto(false)
  }

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

    sesion.cerrar()
  }

  const navegacion = (
    <nav aria-label="Secciones del panel" className="flex flex-col gap-1">
      {visibles.map(({ a, texto, icono: Icono, exacto }) => (
        <NavLink
          key={a}
          to={a}
          end={exacto}
          className={({ isActive }) =>
            `group flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition ${
              isActive
                ? 'bg-emerald-50 text-emerald-800'
                : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900'
            }`
          }
        >
          {({ isActive }) => (
            <>
              <Icono
                aria-hidden
                className={`size-[18px] shrink-0 ${isActive ? 'text-emerald-600' : 'text-slate-400 group-hover:text-slate-600'}`}
              />
              {texto}
            </>
          )}
        </NavLink>
      ))}
    </nav>
  )

  const cuenta = (
    <div className="flex items-center gap-3 rounded-xl border border-slate-200 bg-white p-3">
      <span
        aria-hidden
        className="grid size-9 shrink-0 place-items-center rounded-full bg-emerald-100 text-sm font-semibold text-emerald-800"
      >
        {iniciales(usuario.nombre)}
      </span>
      <div className="min-w-0 flex-1">
        <p className="truncate text-sm font-medium text-slate-900">{usuario.nombre}</p>
        <p className="truncate text-xs text-slate-500">{ROLES[usuario.rol]}</p>
      </div>
      <button
        type="button"
        onClick={() => void salir()}
        title="Salir"
        aria-label="Salir"
        className="grid size-8 place-items-center rounded-lg text-slate-400 transition hover:bg-slate-100 hover:text-slate-700"
      >
        <LogOut aria-hidden className="size-4" />
      </button>
    </div>
  )

  return (
    <div className="min-h-screen bg-slate-50 text-slate-900">
      {/* Escritorio: barra lateral fija. */}
      <aside className="fixed inset-y-0 left-0 z-30 hidden w-64 flex-col border-r border-slate-200 bg-white px-4 py-5 lg:flex">
        <Marca />
        <div className="mt-8 flex-1 overflow-y-auto">{navegacion}</div>
        {cuenta}
      </aside>

      {/* Celular y tablet: barra arriba con el menú en un cajón. */}
      <header className="sticky top-0 z-30 flex items-center justify-between border-b border-slate-200 bg-white/90 px-4 py-3 backdrop-blur lg:hidden">
        <Marca />

        <Dialog.Root open={menuAbierto} onOpenChange={setMenuAbierto}>
          <Dialog.Trigger
            aria-label="Abrir el menú"
            className="grid size-10 place-items-center rounded-lg text-slate-600 transition hover:bg-slate-100"
          >
            <Menu aria-hidden className="size-5" />
          </Dialog.Trigger>

          <Dialog.Portal>
            <Dialog.Overlay className="fixed inset-0 z-40 bg-slate-950/40 backdrop-blur-sm data-[state=closed]:animate-desaparecer data-[state=open]:animate-aparecer" />
            <Dialog.Content className="fixed inset-y-0 left-0 z-50 flex w-72 max-w-[85vw] flex-col bg-white px-4 py-5 shadow-elevada outline-none data-[state=open]:animate-aparecer">
              <div className="flex items-center justify-between">
                <Marca />
                <Dialog.Close
                  aria-label="Cerrar el menú"
                  className="grid size-9 place-items-center rounded-lg text-slate-500 hover:bg-slate-100"
                >
                  <X aria-hidden className="size-5" />
                </Dialog.Close>
              </div>
              <Dialog.Title className="sr-only">Menú del panel</Dialog.Title>
              <Dialog.Description className="sr-only">Secciones del panel de administración</Dialog.Description>
              <div className="mt-8 flex-1 overflow-y-auto">{navegacion}</div>
              {cuenta}
            </Dialog.Content>
          </Dialog.Portal>
        </Dialog.Root>
      </header>

      <main className="lg:pl-64">
        <div className="mx-auto w-full max-w-6xl px-4 py-6 sm:px-6 lg:px-10 lg:py-10">
          {/* Con una contraseña provisoria la API no deja hacer otra cosa: se muestra solo
              el cambio, en vez de un panel donde todo responde 403. */}
          {usuario.debeCambiarPassword ? (
            <CambioDePasswordObligatorio />
          ) : (
            <>
              {usuario.rol === 'Owner' && <AvisoDelPlan />}
              <Outlet />
            </>
          )}
        </div>
      </main>
    </div>
  )
}

function iniciales(nombre: string): string {
  const partes = nombre.trim().split(/\s+/).filter(Boolean)
  const primera = partes[0]?.[0] ?? ''
  const ultima = partes.length > 1 ? partes[partes.length - 1][0] : ''

  return (primera + ultima).toUpperCase() || '?'
}
