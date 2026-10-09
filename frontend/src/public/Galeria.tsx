import { useEffect, useState, type KeyboardEvent } from 'react'
import useEmblaCarousel from 'embla-carousel-react'
import { ChevronLeft, ChevronRight, Expand } from 'lucide-react'
import type { VehiculoFoto } from '@shared/api/types'
import { marcarFoto } from '@public/transicion'

interface Props {
  fotos: VehiculoFoto[]
  titulo: string
  /** Abre el visor a pantalla completa en esa foto. */
  onAmpliar: (indice: number) => void
}

/**
 * La galería de la ficha. Se desliza con el dedo, con las flechas del teclado o con los
 * botones, y cualquier foto se abre a pantalla completa.
 *
 * Mientras la foto grande carga se ve la miniatura de fondo, que ya está en caché por la
 * tarjeta o por la tira de abajo: la galería nunca muestra un hueco gris.
 */
export function Galeria({ fotos, titulo, onAmpliar }: Props) {
  const varias = fotos.length > 1
  const [visor, embla] = useEmblaCarousel({ loop: varias })
  const [tira, emblaDeLaTira] = useEmblaCarousel({ containScroll: 'keepSnaps', dragFree: true })
  const [actual, setActual] = useState(0)

  useEffect(() => {
    if (!embla) return

    const alElegir = () => {
      const indice = embla.selectedScrollSnap()
      setActual(indice)
      emblaDeLaTira?.scrollTo(indice)
    }

    alElegir()
    embla.on('select', alElegir).on('reInit', alElegir)

    return () => {
      embla.off('select', alElegir).off('reInit', alElegir)
    }
  }, [embla, emblaDeLaTira])

  function alTeclear(evento: KeyboardEvent<HTMLDivElement>) {
    if (evento.key === 'ArrowLeft') embla?.scrollPrev()
    if (evento.key === 'ArrowRight') embla?.scrollNext()
  }

  const flecha =
    'absolute top-1/2 grid size-11 -translate-y-1/2 place-items-center rounded-full bg-white/90 text-slate-900 shadow-elevada backdrop-blur transition duration-300 hover:scale-110 md:opacity-0 md:group-hover:opacity-100 md:focus-visible:opacity-100'

  return (
    <div>
      <div
        role="region"
        aria-roledescription="galería"
        aria-label={`Fotos de ${titulo}`}
        tabIndex={0}
        onKeyDown={alTeclear}
        className="group relative overflow-hidden rounded-3xl bg-slate-100 focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-marca"
      >
        <div ref={visor} className="overflow-hidden">
          <div className="flex touch-pan-y">
            {fotos.map((foto, indice) => (
              <div
                key={foto.id}
                className="min-w-0 shrink-0 grow-0 basis-full bg-cover bg-center"
                style={foto.urlThumb ? { backgroundImage: `url("${foto.urlThumb}")` } : undefined}
              >
                <button
                  type="button"
                  onClick={() => onAmpliar(indice)}
                  aria-label={`Ampliar la foto ${indice + 1} de ${fotos.length}`}
                  className="block w-full cursor-zoom-in"
                >
                  <img
                    // La primera foto es la que recibe a la de la tarjeta en la transición.
                    ref={indice === 0 ? marcarFoto : undefined}
                    src={foto.url}
                    alt={indice === 0 ? titulo : ''}
                    loading={indice === 0 ? 'eager' : 'lazy'}
                    fetchPriority={indice === 0 ? 'high' : 'auto'}
                    decoding="async"
                    className="aspect-4/3 w-full object-cover"
                  />
                </button>
              </div>
            ))}
          </div>
        </div>

        {varias && (
          <>
            <button
              type="button"
              onClick={() => embla?.scrollPrev()}
              aria-label="Foto anterior"
              className={`${flecha} left-4`}
            >
              <ChevronLeft className="size-5" aria-hidden />
            </button>
            <button
              type="button"
              onClick={() => embla?.scrollNext()}
              aria-label="Foto siguiente"
              className={`${flecha} right-4`}
            >
              <ChevronRight className="size-5" aria-hidden />
            </button>
          </>
        )}

        <p
          aria-live="polite"
          className="pointer-events-none absolute bottom-4 left-4 rounded-full bg-slate-950/60 px-3 py-1 text-xs font-semibold tabular-nums text-white backdrop-blur"
        >
          {actual + 1} / {fotos.length}
        </p>

        <button
          type="button"
          onClick={() => onAmpliar(actual)}
          aria-label="Ver a pantalla completa"
          className="absolute right-4 top-4 grid size-10 place-items-center rounded-full bg-white/90 text-slate-900 shadow-elevada backdrop-blur transition duration-300 hover:scale-110"
        >
          <Expand className="size-4" aria-hidden />
        </button>
      </div>

      {varias && (
        <div ref={tira} className="mt-3 overflow-hidden">
          <div className="-ml-2 flex">
            {fotos.map((foto, indice) => (
              <div key={foto.id} className="min-w-0 shrink-0 grow-0 basis-1/5 pl-2 sm:basis-1/6">
                <button
                  type="button"
                  onClick={() => embla?.scrollTo(indice)}
                  aria-label={`Ver la foto ${indice + 1}`}
                  aria-current={indice === actual}
                  className={`block w-full overflow-hidden rounded-xl ring-2 ring-offset-2 transition duration-300 ${
                    indice === actual ? 'ring-marca' : 'opacity-60 ring-transparent hover:opacity-100'
                  }`}
                >
                  <img
                    src={foto.urlThumb ?? foto.url}
                    alt=""
                    loading="lazy"
                    decoding="async"
                    className="aspect-4/3 w-full object-cover"
                  />
                </button>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}
