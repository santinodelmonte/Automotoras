import { Link } from 'react-router-dom'
import { ArrowUpRight, MapPin, Phone } from 'lucide-react'
import { linkDeMapa, linkDeWhatsapp } from '@shared/ui/formato'
import type { TenantPublico } from '@shared/api/types'
import { IconoWhatsapp } from '@public/ui/IconoWhatsapp'

interface Props {
  tenant: TenantPublico
  base: string
}

export function Pie({ tenant, base }: Props) {
  return (
    <footer className="mt-24 bg-slate-950 text-slate-400">
      <div className="mx-auto grid max-w-7xl gap-10 px-4 py-14 sm:px-6 md:grid-cols-[2fr_1fr_1fr]">
        <div>
          <p className="flex items-center gap-3 text-lg font-bold text-white">
            {tenant.logoUrl ? (
              <span className="rounded-xl bg-white p-1.5">
                <img src={tenant.logoUrl} alt="" className="h-7 w-auto" />
              </span>
            ) : (
              <span className="grid size-9 place-items-center rounded-xl bg-marca text-base text-marca-contraste">
                {tenant.nombre.charAt(0)}
              </span>
            )}
            {tenant.nombre}
          </p>
          <p className="mt-4 max-w-sm text-sm leading-relaxed">
            Todo lo publicado está disponible. Si una unidad te interesa, escribinos y
            coordinamos para que la veas.
          </p>
        </div>

        <div>
          <h2 className="text-sm font-semibold text-white">Contacto</h2>
          <ul className="mt-4 space-y-3 text-sm">
            {tenant.direccion && (
              <li>
                <a
                  href={linkDeMapa(tenant.direccion)}
                  target="_blank"
                  rel="noreferrer"
                  className="flex gap-2 transition hover:text-white"
                >
                  <MapPin className="mt-0.5 size-4 shrink-0" aria-hidden />
                  {tenant.direccion}
                </a>
              </li>
            )}
            {tenant.telefono && (
              <li>
                <a href={`tel:${tenant.telefono}`} className="flex gap-2 transition hover:text-white">
                  <Phone className="mt-0.5 size-4 shrink-0" aria-hidden />
                  {tenant.telefono}
                </a>
              </li>
            )}
            {tenant.whatsapp && (
              <li>
                <a
                  href={linkDeWhatsapp(tenant.whatsapp, `Hola ${tenant.nombre}, quería hacer una consulta.`)}
                  target="_blank"
                  rel="noreferrer"
                  className="flex gap-2 transition hover:text-white"
                >
                  <IconoWhatsapp className="mt-0.5 size-4 shrink-0" />
                  WhatsApp
                </a>
              </li>
            )}
          </ul>
        </div>

        <div>
          <h2 className="text-sm font-semibold text-white">Explorá</h2>
          <ul className="mt-4 space-y-3 text-sm">
            <li>
              <Link to={base || '/'} viewTransition className="transition hover:text-white">
                Inicio
              </Link>
            </li>
            <li>
              <Link
                to={`${base}/vehiculos`}
                viewTransition
                className="inline-flex items-center gap-1 transition hover:text-white"
              >
                Todos los vehículos
                <ArrowUpRight className="size-3.5" aria-hidden />
              </Link>
            </li>
          </ul>
        </div>
      </div>

      <div className="border-t border-white/10">
        <div className="mx-auto flex max-w-7xl flex-wrap items-center justify-between gap-3 px-4 py-6 text-xs text-slate-400 sm:px-6">
          <p>
            © {new Date().getFullYear()} {tenant.nombre}
          </p>
          <Link to={`${base}/privacidad`} viewTransition className="transition hover:text-white">
            Privacidad
          </Link>
        </div>
      </div>
    </footer>
  )
}
