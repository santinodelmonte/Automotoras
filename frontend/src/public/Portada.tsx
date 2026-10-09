import { useRef, type CSSProperties } from 'react'
import { Link } from 'react-router-dom'
import { etiqueta, precio } from '@shared/ui/formato'
import type { FiltrosDisponibles, VehiculoPublicoResumen } from '@shared/api/types'
import { Buscador } from '@public/Buscador'
import { useSitio } from '@public/TenantContexto'
import { alTocarTarjeta, type EstadoDeFicha } from '@public/transicion'
import { Contador } from '@public/ui/Contador'
import { useProgresoDeScroll } from '@public/ui/movimiento'

interface Props {
  /** Los primeros tres con foto arman el collage: van primero los destacados. */
  vehiculos: VehiculoPublicoResumen[]
  total: number
  filtros: FiltrosDisponibles | null
}

/**
 * Dónde va cada tarjeta del collage, cuánto gira y cuánto sube al bajar la página. Cada
 * una sube a una velocidad distinta: esa diferencia es lo que da la profundidad.
 */
const POSICIONES = [
  { clase: 'left-1/2 top-4 z-20 w-60 -translate-x-1/2 sm:w-72 lg:top-8', giro: -3, recorrido: -70, retraso: 300 },
  { clase: 'left-0 top-28 z-10 w-40 sm:w-52 lg:top-36', giro: -9, recorrido: -25, retraso: 450 },
  { clase: 'right-0 top-44 z-30 w-44 sm:w-56 lg:top-56', giro: 7, recorrido: -140, retraso: 600 },
]

/** El texto entra de a una pieza, con esta pausa entre cada una. */
const PAUSA = 90

/**
 * La portada: qué hay, un buscador y tres autos de verdad del stock flotando al costado.
 *
 * Las fotos del collage son las de la grilla, que ya vienen en la home: la parte más
 * vistosa del sitio no cuesta ningún pedido más.
 */
export function Portada({ vehiculos, total, filtros }: Props) {
  const { tenant, slug } = useSitio()
  const seccion = useProgresoDeScroll<HTMLElement>()

  const base = slug ? `/t/${slug}` : ''
  const collage = vehiculos.filter((vehiculo) => vehiculo.fotoPortadaUrl).slice(0, POSICIONES.length)
  const carrocerias = filtros?.carrocerias ?? []

  const enOrden = (orden: number): CSSProperties => ({ animationDelay: `${orden * PAUSA}ms` })

  return (
    <section ref={seccion} className="relative isolate overflow-hidden bg-linear-to-b from-marca/8 to-white">
      <Fondo />

      <div className="mx-auto grid max-w-7xl items-center gap-12 px-4 pb-20 pt-12 sm:px-6 lg:grid-cols-[1.1fr_1fr] lg:pb-28 lg:pt-20">
        <div>
          <p
            style={enOrden(0)}
            className="motion-safe:animate-entrar inline-flex items-center gap-2 rounded-full bg-white/80 px-3 py-1.5 text-sm font-medium text-slate-700 shadow-sm ring-1 ring-slate-900/5 backdrop-blur"
          >
            <span className="relative flex size-2">
              <span className="absolute inline-flex size-full rounded-full bg-emerald-400 opacity-75 motion-safe:animate-ping" />
              <span className="relative inline-flex size-2 rounded-full bg-emerald-500" />
            </span>
            <span>
              <Contador valor={total} /> {total === 1 ? 'vehículo disponible' : 'vehículos disponibles'}
            </span>
          </p>

          <h1
            style={enOrden(1)}
            className="motion-safe:animate-entrar mt-6 text-4xl font-extrabold leading-[1.05] tracking-tight text-balance text-slate-950 sm:text-5xl lg:text-6xl"
          >
            Encontrá tu próximo auto en <span className="text-marca-tinta">{tenant.nombre}</span>
          </h1>

          <p
            style={enOrden(2)}
            className="motion-safe:animate-entrar mt-5 max-w-xl text-lg text-pretty text-slate-600"
          >
            Mirá el stock completo, filtrá por lo que te importa y consultá por WhatsApp en un
            toque.
          </p>

          <div style={enOrden(3)} className="motion-safe:animate-entrar relative z-40 mt-8 max-w-xl">
            <Buscador filtros={filtros} />
          </div>

          {carrocerias.length > 1 && (
            <div
              style={enOrden(4)}
              className="motion-safe:animate-entrar mt-5 flex flex-wrap items-center gap-2"
            >
              <span className="text-sm text-slate-500">Por tipo:</span>
              {carrocerias.map((carroceria) => (
                <Link
                  key={carroceria}
                  to={`${base}/vehiculos?carroceria=${carroceria}`}
                  viewTransition
                  className="rounded-full bg-white px-3 py-1.5 text-sm font-medium text-slate-700 shadow-sm ring-1 ring-slate-900/5 transition hover:-translate-y-0.5 hover:text-marca-tinta hover:ring-marca/40"
                >
                  {etiqueta(carroceria)}
                </Link>
              ))}
            </div>
          )}
        </div>

        {collage.length > 0 && (
          <div className="relative mx-auto h-88 w-full max-w-md sm:h-104 lg:h-128 lg:max-w-none">
            {collage.map((vehiculo, indice) => (
              <TarjetaFlotante key={vehiculo.id} vehiculo={vehiculo} base={base} indice={indice} />
            ))}
          </div>
        )}
      </div>
    </section>
  )
}

function TarjetaFlotante({
  vehiculo,
  base,
  indice,
}: {
  vehiculo: VehiculoPublicoResumen
  base: string
  indice: number
}) {
  const posicion = POSICIONES[indice]
  const foto = useRef<HTMLImageElement>(null)
  const estado: EstadoDeFicha = { previa: vehiculo }

  // Cuatro movimientos que en un mismo elemento se pisarían, repartidos en tres capas:
  // afuera la posición (`translate`), el parallax (`transform`) y la entrada (`scale`);
  // en el medio la flotación; y en la tarjeta el giro, que se endereza con el mouse.
  return (
    <div
      className={`absolute motion-safe:animate-asomar ${posicion.clase}`}
      style={{
        animationDelay: `${posicion.retraso}ms`,
        transform: `translateY(calc(var(--progreso, 0) * ${posicion.recorrido}px))`,
      }}
    >
      <div className="motion-safe:animate-flotar" style={{ animationDelay: `${indice * -2.4}s` }}>
        <Link
          to={`${base}/vehiculos/${vehiculo.id}`}
          state={estado}
          viewTransition
          onClick={(evento) => alTocarTarjeta(evento, foto.current)}
          style={{ '--giro': `${posicion.giro}deg` } as CSSProperties}
          className="block rotate-(--giro) rounded-2xl bg-white p-2 shadow-elevada ring-1 ring-slate-900/5 transition duration-500 ease-salida hover:rotate-0 hover:scale-105 focus-visible:outline-2 focus-visible:outline-marca"
        >
          <img
            ref={foto}
            src={vehiculo.fotoPortadaUrl ?? undefined}
            alt={`${vehiculo.marca} ${vehiculo.modelo}`}
            fetchPriority={indice === 0 ? 'high' : 'auto'}
            decoding="async"
            className="aspect-4/3 w-full rounded-xl object-cover"
          />
          <div className="flex items-center justify-between gap-2 px-1.5 pb-1 pt-2.5">
            <div className="min-w-0">
              <p className="truncate text-sm font-semibold text-slate-900">
                {vehiculo.marca} {vehiculo.modelo}
              </p>
              <p className="text-xs text-slate-500">{vehiculo.anio}</p>
            </div>
            <p className="shrink-0 text-sm font-bold text-marca-tinta">
              {precio(vehiculo.precio, vehiculo.moneda)}
            </p>
          </div>
        </Link>
      </div>
    </div>
  )
}

/** Manchas del color de la marca y una trama de puntos que se desvanece hacia abajo. */
function Fondo() {
  return (
    <div aria-hidden className="pointer-events-none absolute inset-0 -z-10 overflow-hidden">
      <div className="absolute -right-32 -top-40 size-144 rounded-full bg-marca/15 blur-3xl motion-safe:animate-flotar" />
      <div className="absolute -bottom-48 -left-40 size-120 rounded-full bg-marca/10 blur-3xl motion-safe:animate-flotar [animation-delay:-3.5s]" />
      <div className="absolute inset-0 bg-[radial-gradient(circle_at_1px_1px,rgb(15_23_42/0.08)_1px,transparent_0)] bg-[length:24px_24px] [mask-image:linear-gradient(to_bottom,black,transparent_85%)]" />
    </div>
  )
}
