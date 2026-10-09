import { Phone } from 'lucide-react'
import { IconoWhatsapp } from '@public/ui/IconoWhatsapp'

interface Props {
  precio: string
  linkDeWhatsapp: string | null
  telefono: string | null
  onContactar: (tipo: 'ClickWhatsapp' | 'ClickTelefono') => void
}

/**
 * Precio y contacto fijos abajo, solo en el celular.
 *
 * En una ficha larga, el botón de WhatsApp queda arriba y la persona baja a mirar la ficha
 * técnica y las fotos: cuando se decide, el botón tiene que estar donde tiene el dedo, no
 * tres pantallas más arriba.
 */
export function BarraDeContacto({ precio, linkDeWhatsapp, telefono, onContactar }: Props) {
  if (!linkDeWhatsapp && !telefono) return null

  return (
    <div className="fixed inset-x-0 bottom-0 z-30 border-t border-slate-200 bg-white/95 px-4 pt-3 pb-[max(0.75rem,env(safe-area-inset-bottom))] shadow-[0_-12px_32px_-16px_rgb(15_23_42/0.25)] backdrop-blur [animation-delay:300ms] motion-safe:animate-subir lg:hidden">
      <div className="flex items-center gap-3">
        <div className="min-w-0 flex-1">
          <p className="text-xs text-slate-500">Precio</p>
          <p className="truncate text-lg font-extrabold tracking-tight text-marca-tinta">{precio}</p>
        </div>

        {telefono && (
          <a
            href={`tel:${telefono}`}
            onClick={() => onContactar('ClickTelefono')}
            aria-label={`Llamar al ${telefono}`}
            className="grid size-12 shrink-0 place-items-center rounded-2xl bg-slate-100 text-slate-900 transition active:scale-95"
          >
            <Phone className="size-5" aria-hidden />
          </a>
        )}

        {linkDeWhatsapp && (
          <a
            href={linkDeWhatsapp}
            target="_blank"
            rel="noreferrer"
            onClick={() => onContactar('ClickWhatsapp')}
            className="inline-flex h-12 shrink-0 items-center gap-2 rounded-2xl bg-emerald-700 px-5 font-semibold text-white shadow-sm transition active:scale-95"
          >
            <IconoWhatsapp className="size-5" />
            WhatsApp
          </a>
        )}
      </div>
    </div>
  )
}
