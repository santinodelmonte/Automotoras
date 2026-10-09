import type { LucideIcon } from 'lucide-react'

interface Props {
  titulo: string
  valor: string
  nota?: string
  icono?: LucideIcon
  tono?: 'verde' | 'azul' | 'ambar' | 'violeta' | 'rojo'
}

const TONOS = {
  verde: 'bg-emerald-50 text-emerald-600 ring-emerald-600/10',
  azul: 'bg-sky-50 text-sky-600 ring-sky-600/10',
  ambar: 'bg-amber-50 text-amber-600 ring-amber-600/10',
  violeta: 'bg-violet-50 text-violet-600 ring-violet-600/10',
  rojo: 'bg-rose-50 text-rose-600 ring-rose-600/10',
}

/**
 * Una cifra grande con su rótulo. La usan el tablero y los reportes.
 *
 * Vive en `shared` y no en cada pantalla porque son las dos caras del mismo dato: si el
 * tablero y el reporte de demanda muestran las vistas con tipografías distintas, el dueño
 * asume que están midiendo cosas distintas.
 */
export function Tarjeta({ titulo, valor, nota, icono: Icono, tono = 'verde' }: Props) {
  return (
    <div className="panel-seccion flex items-start justify-between gap-3">
      <div className="min-w-0">
        <p className="text-sm font-medium text-slate-500">{titulo}</p>
        <p className="mt-2 text-3xl font-semibold tracking-tight text-slate-900 tabular-nums">{valor}</p>
        {nota && <p className="mt-1 text-xs text-slate-400">{nota}</p>}
      </div>

      {Icono && (
        <span aria-hidden className={`hidden size-10 shrink-0 place-items-center rounded-xl ring-1 ring-inset sm:grid ${TONOS[tono]}`}>
          <Icono className="size-5" />
        </span>
      )}
    </div>
  )
}
