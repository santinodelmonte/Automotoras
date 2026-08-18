import { Link } from 'react-router-dom'
import { kilometros, precio } from '@shared/ui/formato'
import type { VehiculoPublicoResumen } from '@shared/api/types'

interface Props {
  vehiculo: VehiculoPublicoResumen
  base: string
  colorPrimario: string
}

export function VehiculoCard({ vehiculo, base, colorPrimario }: Props) {
  return (
    <Link
      to={`${base}/vehiculos/${vehiculo.id}`}
      className="group flex flex-col overflow-hidden rounded-xl border border-slate-200 bg-white transition hover:shadow-lg"
    >
      <div className="relative aspect-4/3 overflow-hidden bg-slate-100">
        {vehiculo.fotoPortadaUrl ? (
          <img
            src={vehiculo.fotoPortadaUrl}
            alt={`${vehiculo.marca} ${vehiculo.modelo}`}
            loading="lazy"
            className="h-full w-full object-cover transition group-hover:scale-105"
          />
        ) : (
          <div className="grid h-full place-items-center text-sm text-slate-400">Sin foto</div>
        )}

        {vehiculo.destacado && (
          <span
            className="absolute left-2 top-2 rounded-full px-2 py-0.5 text-xs font-semibold text-white"
            style={{ backgroundColor: colorPrimario }}
          >
            Destacado
          </span>
        )}
      </div>

      <div className="flex flex-1 flex-col gap-1 p-4">
        <p className="text-sm text-slate-500">
          {vehiculo.anio} · {kilometros(vehiculo.kilometraje)}
        </p>
        <p className="font-semibold leading-tight">
          {vehiculo.marca} {vehiculo.modelo}
          {vehiculo.version && <span className="text-slate-500"> {vehiculo.version}</span>}
        </p>
        <p className="mt-auto pt-2 text-lg font-bold" style={{ color: colorPrimario }}>
          {precio(vehiculo.precio, vehiculo.moneda)}
        </p>
      </div>
    </Link>
  )
}
