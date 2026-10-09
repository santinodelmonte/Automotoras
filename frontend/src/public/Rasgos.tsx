import { CalendarDays, Cog, Fuel, Gauge, type LucideIcon } from 'lucide-react'
import { etiqueta, kilometros } from '@shared/ui/formato'

interface Props {
  anio: number
  kilometraje: number
  transmision: string
  combustible: string
  /** En la ficha van más grandes que en la tarjeta. */
  tamano?: 'chico' | 'grande'
}

/** Año, kilómetros, caja y combustible: lo que cualquiera mira antes que el precio. */
export function Rasgos({ anio, kilometraje, transmision, combustible, tamano = 'chico' }: Props) {
  const rasgos: [LucideIcon, string, string][] = [
    [CalendarDays, 'Año', String(anio)],
    [Gauge, 'Kilometraje', kilometros(kilometraje)],
    [Cog, 'Transmisión', etiqueta(transmision)],
    [Fuel, 'Combustible', etiqueta(combustible)],
  ]

  const grande = tamano === 'grande'

  return (
    <ul className={`flex flex-wrap ${grande ? 'gap-2' : 'gap-1.5'}`}>
      {rasgos.map(([Icono, nombre, valor]) => (
        <li
          key={nombre}
          className={`inline-flex items-center gap-1.5 rounded-full bg-slate-100 font-medium text-slate-700 ${
            grande ? 'px-3 py-1.5 text-sm' : 'px-2.5 py-1 text-xs'
          }`}
        >
          <Icono className={`${grande ? 'size-4' : 'size-3.5'} text-slate-500`} aria-hidden />
          <span className="sr-only">{nombre}: </span>
          {valor}
        </li>
      ))}
    </ul>
  )
}
