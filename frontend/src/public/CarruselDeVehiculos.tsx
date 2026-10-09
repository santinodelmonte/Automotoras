import { useEffect, useState } from 'react'
import useEmblaCarousel from 'embla-carousel-react'
import { ChevronLeft, ChevronRight } from 'lucide-react'
import type { VehiculoPublicoResumen } from '@shared/api/types'
import { VehiculoCard } from '@public/VehiculoCard'

interface Props {
  vehiculos: VehiculoPublicoResumen[]
  base: string
  /** Qué es este carrusel, para los lectores de pantalla. */
  etiqueta: string
}

/**
 * Fila deslizable de tarjetas: se arrastra con el dedo en el celular y con las flechas
 * en la compu. En el celular la siguiente tarjeta asoma por el borde, que es lo que avisa
 * que hay más sin tener que explicarlo.
 */
export function CarruselDeVehiculos({ vehiculos, base, etiqueta }: Props) {
  const [visor, embla] = useEmblaCarousel({ align: 'start', containScroll: 'trimSnaps' })
  const [puedeAtras, setPuedeAtras] = useState(false)
  const [puedeAdelante, setPuedeAdelante] = useState(false)

  useEffect(() => {
    if (!embla) return

    const actualizar = () => {
      setPuedeAtras(embla.canScrollPrev())
      setPuedeAdelante(embla.canScrollNext())
    }

    actualizar()
    embla.on('select', actualizar).on('reInit', actualizar)

    return () => {
      embla.off('select', actualizar).off('reInit', actualizar)
    }
  }, [embla])

  return (
    <div className="relative" role="region" aria-roledescription="carrusel" aria-label={etiqueta}>
      {/* El margen vertical deja lugar para la sombra y el salto de la tarjeta al pasar
          el mouse, que el recorte del carrusel cortaría. */}
      <div ref={visor} className="-my-6 overflow-hidden py-6">
        <div className="-ml-5 flex touch-pan-y">
          {vehiculos.map((vehiculo) => (
            <div
              key={vehiculo.id}
              className="min-w-0 shrink-0 grow-0 basis-[82%] pl-5 sm:basis-1/2 lg:basis-1/3 xl:basis-1/4"
            >
              <VehiculoCard vehiculo={vehiculo} base={base} />
            </div>
          ))}
        </div>
      </div>

      <Flecha lado="atras" visible={puedeAtras} onClick={() => embla?.scrollPrev()} />
      <Flecha lado="adelante" visible={puedeAdelante} onClick={() => embla?.scrollNext()} />
    </div>
  )
}

function Flecha({
  lado,
  visible,
  onClick,
}: {
  lado: 'atras' | 'adelante'
  visible: boolean
  onClick: () => void
}) {
  const Icono = lado === 'atras' ? ChevronLeft : ChevronRight

  return (
    <button
      type="button"
      onClick={onClick}
      aria-label={lado === 'atras' ? 'Ver anteriores' : 'Ver siguientes'}
      aria-hidden={!visible}
      tabIndex={visible ? 0 : -1}
      className={`absolute top-1/2 z-10 hidden size-11 -translate-y-1/2 place-items-center rounded-full bg-white text-slate-900 shadow-elevada ring-1 ring-slate-900/5 transition duration-300 hover:scale-110 md:grid ${
        lado === 'atras' ? '-left-4' : '-right-4'
      } ${visible ? 'opacity-100' : 'pointer-events-none opacity-0'}`}
    >
      <Icono className="size-5" aria-hidden />
    </button>
  )
}
