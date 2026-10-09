import { useRef } from 'react'
import { Link } from 'react-router-dom'
import { ArrowUpRight, Camera, Sparkles } from 'lucide-react'
import { precio } from '@shared/ui/formato'
import type { VehiculoPublicoResumen } from '@shared/api/types'
import { Rasgos } from '@public/Rasgos'
import { alTocarTarjeta, type EstadoDeFicha } from '@public/transicion'

interface Props {
  vehiculo: VehiculoPublicoResumen
  base: string
  /** Las primeras tarjetas se piden con prioridad: son lo primero que se ve. */
  prioridad?: boolean
}

export function VehiculoCard({ vehiculo, base, prioridad = false }: Props) {
  const foto = useRef<HTMLImageElement>(null)
  const estado: EstadoDeFicha = { previa: vehiculo }

  return (
    <Link
      to={`${base}/vehiculos/${vehiculo.id}`}
      state={estado}
      viewTransition
      onClick={(evento) => alTocarTarjeta(evento, foto.current)}
      className="group flex h-full flex-col overflow-hidden rounded-2xl bg-white shadow-tarjeta ring-1 ring-slate-900/5 transition duration-300 ease-salida hover:-translate-y-1 hover:shadow-elevada focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-marca"
    >
      <div className="relative aspect-4/3 overflow-hidden bg-slate-100">
        {vehiculo.fotoPortadaUrl ? (
          <img
            ref={foto}
            src={vehiculo.fotoPortadaUrl}
            alt={`${vehiculo.marca} ${vehiculo.modelo}`}
            loading={prioridad ? 'eager' : 'lazy'}
            fetchPriority={prioridad ? 'high' : 'auto'}
            decoding="async"
            className="size-full object-cover transition duration-700 ease-salida group-hover:scale-[1.06]"
          />
        ) : (
          <div className="grid size-full place-items-center text-slate-400">
            <Camera className="size-8" aria-hidden />
            <span className="sr-only">Sin foto</span>
          </div>
        )}

        <div
          aria-hidden
          className="pointer-events-none absolute inset-x-0 bottom-0 h-24 bg-linear-to-t from-slate-950/35 to-transparent opacity-0 transition duration-300 group-hover:opacity-100"
        />

        {vehiculo.destacado && (
          <span className="absolute left-3 top-3 inline-flex items-center gap-1 rounded-full bg-marca px-2.5 py-1 text-xs font-semibold text-marca-contraste shadow-sm">
            <Sparkles className="size-3.5" aria-hidden />
            Destacado
          </span>
        )}
      </div>

      <div className="flex flex-1 flex-col gap-3 p-4">
        <div>
          <h3 className="font-semibold leading-snug text-slate-900">
            {vehiculo.marca} {vehiculo.modelo}
          </h3>
          {vehiculo.version && <p className="mt-0.5 truncate text-sm text-slate-500">{vehiculo.version}</p>}
        </div>

        <Rasgos
          anio={vehiculo.anio}
          kilometraje={vehiculo.kilometraje}
          transmision={vehiculo.transmision}
          combustible={vehiculo.combustible}
        />

        <div className="mt-auto flex items-end justify-between gap-3 pt-1">
          <p className="text-xl font-bold tracking-tight text-marca-tinta">
            {precio(vehiculo.precio, vehiculo.moneda)}
          </p>
          <span
            aria-hidden
            className="grid size-9 shrink-0 place-items-center rounded-full bg-slate-100 text-slate-600 transition duration-300 group-hover:bg-marca group-hover:text-marca-contraste"
          >
            <ArrowUpRight className="size-4 transition duration-300 group-hover:rotate-45" />
          </span>
        </div>
      </div>
    </Link>
  )
}
