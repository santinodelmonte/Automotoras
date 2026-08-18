import type { Comparativo } from '@shared/api/types'

/**
 * En qué dirección está bien estar. Tardar menos días en vender es mejor; recibir más
 * consultas cada cien visitas también. Sin esto las barras se pintarían al revés en una de
 * las dos métricas.
 */
export type Direccion = 'menosEsMejor' | 'masEsMejor'

const NEUTRO = 'bg-slate-400'
const BIEN = 'bg-emerald-500'
const MAL = 'bg-amber-500'

/**
 * Un indicador propio contra el del resto del mercado, en dos barras a la misma escala.
 *
 * La escala es común a las dos barras a propósito: dos barras normalizadas por separado se
 * verían iguales aunque los números difieran por el doble.
 */
export function ComparativoDeMercado({
  comparativo,
  direccion,
  unidad,
}: {
  comparativo: Comparativo
  direccion: Direccion
  unidad: string
}) {
  const { propio, mercado, lectura, automotorasAportantes } = comparativo
  const tope = Math.max(propio ?? 0, mercado, 1)

  const mejor =
    propio === null
      ? null
      : direccion === 'menosEsMejor'
        ? propio < mercado
        : propio > mercado

  return (
    <li className="rounded-xl border border-slate-200 bg-white p-4">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h3 className="font-semibold">{comparativo.dimension}</h3>
        <p className="text-xs text-slate-400">
          promedio de {automotorasAportantes} automotoras
        </p>
      </div>

      <div className="mt-3 flex flex-col gap-2">
        <Barra
          etiqueta="Vos"
          valor={propio}
          tope={tope}
          unidad={unidad}
          color={mejor === null ? NEUTRO : mejor ? BIEN : MAL}
        />
        <Barra etiqueta="Mercado" valor={mercado} tope={tope} unidad={unidad} color={NEUTRO} />
      </div>

      <p className="mt-3 text-sm text-slate-600">{lectura}</p>
    </li>
  )
}

function Barra({
  etiqueta,
  valor,
  tope,
  unidad,
  color,
}: {
  etiqueta: string
  valor: number | null
  tope: number
  unidad: string
  color: string
}) {
  return (
    <div className="flex items-center gap-3">
      <span className="w-16 shrink-0 text-xs uppercase tracking-wide text-slate-400">
        {etiqueta}
      </span>

      <div className="h-2 flex-1 overflow-hidden rounded-full bg-slate-100">
        {valor !== null && (
          <div
            className={`h-full rounded-full ${color}`}
            style={{ width: `${Math.min((valor / tope) * 100, 100)}%` }}
          />
        )}
      </div>

      <span className="w-28 shrink-0 text-right text-sm font-medium text-slate-700">
        {valor === null ? 'sin datos' : `${valor.toLocaleString('es-UY')} ${unidad}`}
      </span>
    </div>
  )
}
