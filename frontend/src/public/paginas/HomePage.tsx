import { useEffect, useState, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { ArrowRight, Car } from 'lucide-react'
import { api } from '@shared/api/client'
import { Esqueleto, Estado } from '@shared/ui/Estado'
import { etiqueta, linkDeWhatsapp } from '@shared/ui/formato'
import type { FiltrosDisponibles, HomePublica, TenantPublico, VehiculoPublicoResumen } from '@shared/api/types'
import { CarruselDeVehiculos } from '@public/CarruselDeVehiculos'
import { Portada } from '@public/Portada'
import { useSitio } from '@public/TenantContexto'
import { VehiculoCard } from '@public/VehiculoCard'
import { Aparecer } from '@public/ui/Aparecer'
import { Contador } from '@public/ui/Contador'
import { IconoWhatsapp } from '@public/ui/IconoWhatsapp'

export function HomePage() {
  const { tenant, slug } = useSitio()
  const [home, setHome] = useState<HomePublica | null>(null)
  const [filtros, setFiltros] = useState<FiltrosDisponibles | null>(null)
  const [error, setError] = useState<string | null>(null)

  const base = slug ? `/t/${slug}` : ''

  useEffect(() => {
    const controlador = new AbortController()

    api.publico
      .home(slug, controlador.signal)
      .then(setHome)
      .catch((problema: unknown) => {
        if (controlador.signal.aborted) return
        setError(problema instanceof Error ? problema.message : 'No se pudo cargar el catálogo.')
      })

    return () => controlador.abort()
  }, [slug])

  // Los filtros alimentan el buscador, los tipos de carrocería y las cifras. Si fallan,
  // la home se ve igual sin esas partes: no vale la pena una pantalla de error por eso.
  useEffect(() => {
    const controlador = new AbortController()

    api.publico
      .filtros(slug, controlador.signal)
      .then(setFiltros)
      .catch(() => undefined)

    return () => controlador.abort()
  }, [slug])

  useEffect(() => {
    document.title = tenant.nombre
  }, [tenant.nombre])

  if (error) return <Estado titulo="No pudimos cargar el catálogo" detalle={error} />

  if (!home) return <EsqueletoDeInicio />

  if (home.totalDisponibles === 0) return <SinStock tenant={tenant} />

  const vidriera = [...home.destacados, ...home.recientes]

  return (
    <>
      <Portada vehiculos={vidriera} total={home.totalDisponibles} filtros={filtros} />

      <Cifras total={home.totalDisponibles} filtros={filtros} />

      {home.destacados.length > 0 && (
        <Seccion
          titulo="Destacados"
          bajada={`Las unidades que ${tenant.nombre} eligió mostrar primero.`}
          base={base}
        >
          <CarruselDeVehiculos vehiculos={home.destacados} base={base} etiqueta="Destacados" />
        </Seccion>
      )}

      {filtros && filtros.carrocerias.length > 1 && (
        <Seccion titulo="Explorá por tipo" bajada="Elegí la carrocería y mirá solo eso." base={base}>
          <Carrocerias carrocerias={filtros.carrocerias} vehiculos={vidriera} base={base} />
        </Seccion>
      )}

      {home.recientes.length > 0 && (
        <Seccion titulo="Recién ingresados" bajada="Lo último que entró al stock." base={base}>
          <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-4">
            {home.recientes.map((vehiculo, indice) => (
              <Aparecer key={vehiculo.id} retraso={(indice % 4) * 0.08} className="h-full">
                <VehiculoCard vehiculo={vehiculo} base={base} />
              </Aparecer>
            ))}
          </div>
        </Seccion>
      )}

      {tenant.whatsapp && <LlamadaFinal whatsapp={tenant.whatsapp} />}
    </>
  )
}

function Seccion({
  titulo,
  bajada,
  base,
  children,
}: {
  titulo: string
  bajada: string
  base: string
  children: ReactNode
}) {
  return (
    <section className="mx-auto mt-20 max-w-7xl px-4 sm:px-6 lg:mt-24">
      <Aparecer className="mb-8 flex flex-wrap items-end justify-between gap-4">
        <div>
          <h2 className="text-2xl font-bold tracking-tight text-slate-950 sm:text-3xl">{titulo}</h2>
          <p className="mt-1.5 text-slate-500">{bajada}</p>
        </div>
        <Link
          to={`${base}/vehiculos`}
          viewTransition
          className="group inline-flex items-center gap-1.5 text-sm font-semibold text-marca-tinta"
        >
          Ver todos
          <ArrowRight className="size-4 transition group-hover:translate-x-1" aria-hidden />
        </Link>
      </Aparecer>

      {children}
    </section>
  )
}

/**
 * Cifras del stock. Salen todas de lo publicado, ninguna es un texto de marketing: el
 * sitio de una automotora que dice "más de 500 clientes felices" y no lo puede probar
 * pierde más confianza de la que gana.
 */
function Cifras({ total, filtros }: { total: number; filtros: FiltrosDisponibles | null }) {
  const cifras: { valor: ReactNode; texto: string }[] = [
    { valor: <Contador valor={total} />, texto: total === 1 ? 'vehículo disponible' : 'vehículos disponibles' },
  ]

  if (filtros && filtros.marcas.length > 0) {
    cifras.push({
      valor: <Contador valor={filtros.marcas.length} />,
      texto: filtros.marcas.length === 1 ? 'marca' : 'marcas distintas',
    })
  }

  if (filtros?.anioMinimo && filtros.anioMaximo) {
    cifras.push({
      valor:
        filtros.anioMinimo === filtros.anioMaximo
          ? String(filtros.anioMinimo)
          : `${filtros.anioMinimo}–${filtros.anioMaximo}`,
      texto: 'años de los modelos',
    })
  }

  if (filtros && filtros.carrocerias.length > 1) {
    cifras.push({ valor: <Contador valor={filtros.carrocerias.length} />, texto: 'tipos de carrocería' })
  }

  return (
    <section className="border-y border-slate-200 bg-slate-50/70">
      <dl className="mx-auto grid max-w-7xl grid-cols-2 px-4 sm:px-6 lg:grid-cols-4 lg:divide-x lg:divide-slate-200">
        {cifras.map((cifra, indice) => (
          <Aparecer key={cifra.texto} retraso={indice * 0.08} className="flex flex-col-reverse px-2 py-7 text-center lg:py-9">
            <dt className="mt-1 text-sm text-slate-500">{cifra.texto}</dt>
            <dd className="text-3xl font-extrabold tracking-tight text-slate-950 sm:text-4xl">{cifra.valor}</dd>
          </Aparecer>
        ))}
      </dl>
    </section>
  )
}

/** Un mosaico por carrocería, con la foto de un auto de ese tipo que está en el stock. */
function Carrocerias({
  carrocerias,
  vehiculos,
  base,
}: {
  carrocerias: string[]
  vehiculos: VehiculoPublicoResumen[]
  base: string
}) {
  return (
    <div className="grid grid-cols-2 gap-4 md:grid-cols-3 lg:grid-cols-4">
      {carrocerias.map((carroceria, indice) => {
        const ejemplo = vehiculos.find((v) => v.carroceria === carroceria && v.fotoPortadaUrl)

        return (
          <Aparecer key={carroceria} retraso={(indice % 4) * 0.06}>
            <Link
              to={`${base}/vehiculos?carroceria=${carroceria}`}
              viewTransition
              className="group relative block aspect-4/3 overflow-hidden rounded-2xl bg-slate-100 ring-1 ring-slate-900/5 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-marca"
            >
              {ejemplo?.fotoPortadaUrl ? (
                <img
                  src={ejemplo.fotoPortadaUrl}
                  alt=""
                  loading="lazy"
                  decoding="async"
                  className="size-full object-cover transition duration-700 ease-salida group-hover:scale-110"
                />
              ) : (
                <div className="grid size-full place-items-center bg-marca/10">
                  <Car className="size-10 text-marca-tinta" aria-hidden />
                </div>
              )}
              <div
                aria-hidden
                className="absolute inset-0 bg-linear-to-t from-slate-950/75 via-slate-950/10 to-transparent"
              />
              <div className="absolute inset-x-0 bottom-0 flex items-center justify-between gap-2 p-4 text-white">
                <span className="text-lg font-bold">{etiqueta(carroceria)}</span>
                <span className="grid size-8 place-items-center rounded-full bg-white/20 backdrop-blur transition duration-300 group-hover:bg-white group-hover:text-slate-900">
                  <ArrowRight className="size-4" aria-hidden />
                </span>
              </div>
            </Link>
          </Aparecer>
        )
      })}
    </div>
  )
}

/**
 * El cierre de la home: lo que no está publicado también se puede pedir. Es la misma
 * demanda que miden las búsquedas sin resultado, pero llega como una conversación.
 */
function LlamadaFinal({ whatsapp }: { whatsapp: string }) {
  return (
    <section className="mx-auto mt-24 max-w-7xl px-4 sm:px-6">
      <Aparecer>
        <div className="relative isolate overflow-hidden rounded-3xl bg-marca px-6 py-14 text-center text-marca-contraste sm:px-12 lg:py-20">
          <div aria-hidden className="absolute -right-20 -top-24 -z-10 size-80 rounded-full bg-white/15 blur-2xl" />
          <div aria-hidden className="absolute -bottom-32 -left-16 -z-10 size-96 rounded-full bg-black/10 blur-2xl" />

          <h2 className="text-3xl font-extrabold tracking-tight text-balance sm:text-4xl">
            ¿No encontrás lo que buscás?
          </h2>
          <p className="mx-auto mt-3 max-w-xl text-lg opacity-90">
            Contanos qué auto querés y te avisamos cuando entre uno así.
          </p>
          <a
            href={linkDeWhatsapp(whatsapp, 'Hola, estoy buscando un auto que no vi en el sitio: ')}
            target="_blank"
            rel="noreferrer"
            className="mt-8 inline-flex items-center gap-2 rounded-full bg-white px-6 py-3.5 font-semibold text-slate-900 shadow-elevada transition duration-300 hover:scale-105"
          >
            <IconoWhatsapp className="size-5 text-emerald-600" />
            Escribinos por WhatsApp
          </a>
        </div>
      </Aparecer>
    </section>
  )
}

function SinStock({ tenant }: { tenant: TenantPublico }) {
  return (
    <div className="mx-auto max-w-2xl px-4 py-24 text-center">
      <span className="mx-auto grid size-16 place-items-center rounded-2xl bg-marca/10 text-marca-tinta">
        <Car className="size-8" aria-hidden />
      </span>
      <h1 className="mt-6 text-3xl font-bold tracking-tight">Todavía no hay vehículos publicados</h1>
      <p className="mt-3 text-slate-600">Escribinos y contanos qué estás buscando.</p>
      {tenant.whatsapp && (
        <a
          href={linkDeWhatsapp(tenant.whatsapp, 'Hola, estoy buscando un auto: ')}
          target="_blank"
          rel="noreferrer"
          className="mt-8 inline-flex items-center gap-2 rounded-full bg-emerald-600 px-6 py-3 font-semibold text-white transition hover:bg-emerald-700"
        >
          <IconoWhatsapp className="size-5" />
          Escribinos por WhatsApp
        </a>
      )}
    </div>
  )
}

function EsqueletoDeInicio() {
  return (
    <div className="mx-auto max-w-7xl px-4 pt-12 sm:px-6 lg:pt-20">
      <div className="grid items-center gap-12 lg:grid-cols-[1.1fr_1fr]">
        <div className="space-y-5">
          <Esqueleto className="h-8 w-56 rounded-full" />
          <Esqueleto className="h-14 w-full" />
          <Esqueleto className="h-14 w-4/5" />
          <Esqueleto className="h-14 w-full rounded-2xl" />
        </div>
        <Esqueleto className="h-80 rounded-3xl lg:h-112" />
      </div>
      <div className="mt-20 grid gap-5 sm:grid-cols-2 lg:grid-cols-4">
        {[0, 1, 2, 3].map((i) => (
          <Esqueleto key={i} className="h-80 rounded-2xl" />
        ))}
      </div>
    </div>
  )
}
