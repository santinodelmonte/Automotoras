import { Link, NavLink } from 'react-router-dom'
import { Phone } from 'lucide-react'
import { linkDeWhatsapp } from '@shared/ui/formato'
import type { TenantPublico } from '@shared/api/types'
import { IconoWhatsapp } from '@public/ui/IconoWhatsapp'
import { useHayScroll } from '@public/ui/useHayScroll'

interface Props {
  tenant: TenantPublico
  base: string
}

/**
 * Cabecera fija con el WhatsApp siempre a mano.
 *
 * Arriba de todo es blanca y lisa; al bajar se vuelve translúcida y se separa con una
 * línea, porque ahí el contenido pasa por detrás y tiene que distinguirse.
 */
export function Cabecera({ tenant, base }: Props) {
  const conScroll = useHayScroll(8)

  return (
    <header
      className={`sticky top-0 z-40 border-b transition-[background-color,border-color,box-shadow] duration-300 [view-transition-name:cabecera] ${
        conScroll
          ? 'border-slate-200/80 bg-white/85 shadow-sm backdrop-blur-md'
          : 'border-transparent bg-white'
      }`}
    >
      <div className="mx-auto flex h-16 max-w-7xl items-center justify-between gap-4 px-4 sm:px-6">
        <Link to={base || '/'} viewTransition className="flex min-w-0 items-center gap-3">
          {tenant.logoUrl ? (
            <img src={tenant.logoUrl} alt={tenant.nombre} className="h-9 w-auto" />
          ) : (
            <span className="grid size-9 shrink-0 place-items-center rounded-xl bg-marca text-base font-bold text-marca-contraste">
              {tenant.nombre.charAt(0)}
            </span>
          )}
          <span className="truncate text-base font-bold tracking-tight sm:text-lg">{tenant.nombre}</span>
        </Link>

        <nav className="flex shrink-0 items-center gap-1 sm:gap-2">
          <NavLink
            to={`${base}/vehiculos`}
            viewTransition
            className={({ isActive }) =>
              `rounded-full px-3 py-2 text-sm font-medium transition ${
                isActive ? 'bg-slate-100 text-slate-900' : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900'
              }`
            }
          >
            Vehículos
          </NavLink>

          {tenant.telefono && (
            <a
              href={`tel:${tenant.telefono}`}
              className="hidden items-center gap-2 rounded-full px-3 py-2 text-sm font-medium text-slate-600 transition hover:bg-slate-100 hover:text-slate-900 md:inline-flex"
            >
              <Phone className="size-4" aria-hidden />
              {tenant.telefono}
            </a>
          )}

          {tenant.whatsapp && (
            <a
              href={linkDeWhatsapp(tenant.whatsapp, `Hola ${tenant.nombre}, quería hacer una consulta.`)}
              target="_blank"
              rel="noreferrer"
              className="inline-flex items-center gap-2 rounded-full bg-emerald-700 px-3 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-emerald-800 sm:px-4"
            >
              <IconoWhatsapp className="size-4" />
              <span className="sr-only sm:not-sr-only">WhatsApp</span>
            </a>
          )}
        </nav>
      </div>
    </header>
  )
}
