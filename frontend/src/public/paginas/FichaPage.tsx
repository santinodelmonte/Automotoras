import { lazy, Suspense, useEffect, useState } from 'react'
import { Link, useLocation, useParams } from 'react-router-dom'
import {
  ArrowUpRight,
  CalendarDays,
  Camera,
  Car,
  Check,
  ChevronRight,
  Cog,
  DoorOpen,
  Fuel,
  Gauge,
  MapPin,
  Palette,
  Phone,
  Share2,
  Sparkles,
  Zap,
  type LucideIcon,
} from 'lucide-react'
import { api, ApiError } from '@shared/api/client'
import { idDeVisita } from '@shared/analitica/sesion'
import { Esqueleto, Estado } from '@shared/ui/Estado'
import { etiqueta, kilometros, linkDeMapa, linkDeWhatsapp, precio } from '@shared/ui/formato'
import type { VehiculoPublico, VehiculoPublicoResumen } from '@shared/api/types'
import { BarraDeContacto } from '@public/BarraDeContacto'
import { Galeria } from '@public/Galeria'
import { OtrasUnidades } from '@public/OtrasUnidades'
import { Rasgos } from '@public/Rasgos'
import { useSitio } from '@public/TenantContexto'
import { marcarFoto, type EstadoDeFicha } from '@public/transicion'
import { Aparecer } from '@public/ui/Aparecer'
import { IconoWhatsapp } from '@public/ui/IconoWhatsapp'

const VisorDeFotos = lazy(() => import('@public/VisorDeFotos'))

export function FichaPage() {
  const { tenant, slug } = useSitio()
  const { id } = useParams<{ id: string }>()
  const { state } = useLocation()

  const [vehiculo, setVehiculo] = useState<VehiculoPublico | null>(null)
  const [error, setError] = useState<'no-esta' | 'falla' | null>(null)
  const [ampliada, setAmpliada] = useState<number | null>(null)

  const base = slug ? `/t/${slug}` : ''
  const vehiculoId = Number(id)

  // Lo que mandó la tarjeta, si se llegó desde una: con eso se pinta foto, título y
  // precio en el acto. Si se entró por un link, no hay y se espera a la API.
  const previa = (state as EstadoDeFicha | null)?.previa
  const resumen = previa?.id === vehiculoId ? previa : undefined

  useEffect(() => {
    if (!Number.isFinite(vehiculoId)) {
      setError('no-esta')
      return
    }

    const controlador = new AbortController()
    setVehiculo(null)
    setError(null)
    setAmpliada(null)

    api.publico
      .vehiculo(slug, vehiculoId, controlador.signal)
      .then((encontrado) => {
        setVehiculo(encontrado)

        // La vista se registra recién cuando la ficha existe y se pudo mostrar. Contar
        // vistas de fichas que devolvieron 404 inflaría el reporte con tráfico que nunca
        // vio nada.
        void api.publico.evento(slug, {
          tipo: 'ViewFicha',
          vehiculoId,
          sessionId: idDeVisita(),
        })
      })
      .catch((problema: unknown) => {
        if (controlador.signal.aborted) return
        setError(problema instanceof ApiError && problema.status === 404 ? 'no-esta' : 'falla')
      })

    return () => controlador.abort()
  }, [slug, vehiculoId])

  // Meta tags por vehículo. En una SPA esto alcanza para lo que ve la persona y para los
  // buscadores que ejecutan JavaScript; los crawlers de Open Graph que no lo hacen
  // necesitan prerenderizado, que no es parte de la fase 1.
  useEffect(() => {
    if (!vehiculo) return

    document.title = vehiculo.titulo

    const descripcion = `${vehiculo.marca} ${vehiculo.modelo} ${vehiculo.anio}, ${kilometros(
      vehiculo.kilometraje,
    )}, ${precio(vehiculo.precio, vehiculo.moneda)}. ${tenant.nombre}.`

    ponerMeta('description', descripcion)
    ponerMeta('og:title', vehiculo.titulo, true)
    ponerMeta('og:description', descripcion, true)
    ponerMeta('og:type', 'product', true)

    if (vehiculo.fotos[0]) {
      ponerMeta('og:image', vehiculo.fotos[0].url, true)
    }
  }, [vehiculo, tenant.nombre])

  if (error === 'no-esta') {
    return (
      <Estado
        titulo="Este vehículo ya no está publicado"
        detalle="Puede que se haya vendido. Mirá el resto del stock."
      >
        <Link
          to={`${base}/vehiculos`}
          viewTransition
          className="mt-2 rounded-full bg-marca px-5 py-2.5 font-semibold text-marca-contraste"
        >
          Ver vehículos
        </Link>
      </Estado>
    )
  }

  if (error === 'falla') {
    return <Estado titulo="No pudimos cargar el vehículo" detalle="Probá de nuevo en un momento." />
  }

  const datos = vehiculo ?? resumen

  if (!datos) return <EsqueletoDeFicha />

  function contactar(tipo: 'ClickWhatsapp' | 'ClickTelefono') {
    void api.publico.evento(slug, { tipo, vehiculoId, sessionId: idDeVisita() })
  }

  // El mensaje lo arma el servidor, así que el botón aparece cuando llega la ficha.
  const whatsapp =
    vehiculo && tenant.whatsapp ? linkDeWhatsapp(tenant.whatsapp, vehiculo.mensajeDeWhatsapp) : null

  return (
    <article className="mx-auto max-w-7xl px-4 pb-32 pt-6 sm:px-6 lg:pb-8 lg:pt-8">
      <nav aria-label="Ruta" className="flex min-w-0 items-center gap-1.5 text-sm text-slate-500">
        <Link to={base || '/'} viewTransition className="shrink-0 transition hover:text-slate-900">
          Inicio
        </Link>
        <ChevronRight className="size-4 shrink-0" aria-hidden />
        <Link to={`${base}/vehiculos`} viewTransition className="shrink-0 transition hover:text-slate-900">
          Vehículos
        </Link>
        <ChevronRight className="size-4 shrink-0" aria-hidden />
        <span className="truncate font-medium text-slate-900">
          {datos.marca} {datos.modelo}
        </span>
      </nav>

      {/* En el celular va foto, precio y contacto, y después el detalle. En la compu el
          precio y el contacto quedan fijos a la derecha mientras se recorre todo lo demás. */}
      <div className="mt-5 grid gap-8 lg:grid-cols-[minmax(0,3fr)_minmax(0,2fr)] lg:gap-x-10 lg:gap-y-14">
        <div className="min-w-0">
          {vehiculo ? (
            vehiculo.fotos.length > 0 ? (
              <Galeria fotos={vehiculo.fotos} titulo={vehiculo.titulo} onAmpliar={setAmpliada} />
            ) : (
              <SinFotos />
            )
          ) : (
            <FotoPrevia resumen={resumen} />
          )}
        </div>

        <aside className="lg:col-start-2 lg:row-span-2 lg:row-start-1">
          <div className="rounded-3xl bg-white p-6 shadow-tarjeta ring-1 ring-slate-900/5 sm:p-8 lg:sticky lg:top-24">
            {datos.destacado && (
              <span className="mb-4 inline-flex items-center gap-1 rounded-full bg-marca/10 px-2.5 py-1 text-xs font-semibold text-marca-tinta">
                <Sparkles className="size-3.5" aria-hidden />
                Destacado
              </span>
            )}

            <h1 className="text-3xl font-extrabold tracking-tight text-balance text-slate-950">
              {datos.marca} {datos.modelo}
            </h1>
            {datos.version && <p className="mt-1 text-lg text-slate-500">{datos.version}</p>}

            <div className="mt-5">
              <Rasgos
                tamano="grande"
                anio={datos.anio}
                kilometraje={datos.kilometraje}
                transmision={datos.transmision}
                combustible={datos.combustible}
              />
            </div>

            <p className="mt-7 text-sm font-medium text-slate-500">Precio</p>
            <p className="text-4xl font-extrabold tracking-tight text-marca-tinta sm:text-5xl">
              {precio(datos.precio, datos.moneda)}
            </p>

            <div className="mt-7 flex flex-col gap-3">
              {!vehiculo && <Esqueleto className="h-14 rounded-2xl" />}

              {whatsapp && (
                <a
                  href={whatsapp}
                  target="_blank"
                  rel="noreferrer"
                  onClick={() => contactar('ClickWhatsapp')}
                  className="inline-flex items-center justify-center gap-2 rounded-2xl bg-emerald-600 px-6 py-4 text-base font-semibold text-white shadow-sm transition hover:bg-emerald-700 hover:shadow-md active:scale-[0.99]"
                >
                  <IconoWhatsapp className="size-5" />
                  Consultar por WhatsApp
                </a>
              )}

              {tenant.telefono && (
                <a
                  href={`tel:${tenant.telefono}`}
                  onClick={() => contactar('ClickTelefono')}
                  className="inline-flex items-center justify-center gap-2 rounded-2xl px-6 py-4 font-semibold text-slate-900 ring-1 ring-slate-200 transition hover:ring-slate-400 active:scale-[0.99]"
                >
                  <Phone className="size-5" aria-hidden />
                  Llamar al {tenant.telefono}
                </a>
              )}

              <BotonCompartir titulo={`${datos.marca} ${datos.modelo} ${datos.anio}`} />
            </div>

            {tenant.direccion && (
              <a
                href={linkDeMapa(tenant.direccion)}
                target="_blank"
                rel="noreferrer"
                className="group mt-6 flex items-start gap-3 rounded-2xl bg-slate-50 p-4 text-sm transition hover:bg-slate-100"
              >
                <span className="grid size-9 shrink-0 place-items-center rounded-xl bg-white text-marca-tinta shadow-sm ring-1 ring-slate-900/5">
                  <MapPin className="size-4" aria-hidden />
                </span>
                <span className="min-w-0 flex-1">
                  <span className="block font-semibold text-slate-900">Vení a verlo</span>
                  <span className="text-slate-600">{tenant.direccion}</span>
                </span>
                <ArrowUpRight
                  className="size-4 shrink-0 text-slate-400 transition group-hover:text-slate-900"
                  aria-hidden
                />
              </a>
            )}
          </div>
        </aside>

        <div className="min-w-0 lg:col-start-1">
          {vehiculo ? <Detalle vehiculo={vehiculo} /> : <Esqueleto className="h-48 rounded-3xl" />}
        </div>
      </div>

      {vehiculo && <OtrasUnidades vehiculo={vehiculo} />}

      <BarraDeContacto
        precio={precio(datos.precio, datos.moneda)}
        linkDeWhatsapp={whatsapp}
        telefono={tenant.telefono}
        onContactar={contactar}
      />

      {vehiculo && ampliada !== null && (
        <Suspense fallback={null}>
          <VisorDeFotos
            fotos={vehiculo.fotos}
            indice={ampliada}
            titulo={vehiculo.titulo}
            onCerrar={() => setAmpliada(null)}
          />
        </Suspense>
      )}
    </article>
  )
}

/** La foto que trajo la tarjeta, mientras llega la galería completa. */
function FotoPrevia({ resumen }: { resumen: VehiculoPublicoResumen | undefined }) {
  if (!resumen?.fotoPortadaUrl) return <Esqueleto className="aspect-4/3 rounded-3xl" />

  return (
    <div className="overflow-hidden rounded-3xl bg-slate-100">
      <img
        ref={marcarFoto}
        src={resumen.fotoPortadaUrl}
        alt={`${resumen.marca} ${resumen.modelo}`}
        className="aspect-4/3 w-full object-cover"
      />
    </div>
  )
}

function SinFotos() {
  return (
    <div className="grid aspect-4/3 place-items-center rounded-3xl bg-slate-100 text-slate-400">
      <span className="flex flex-col items-center gap-2 text-sm">
        <Camera className="size-8" aria-hidden />
        Sin fotos
      </span>
    </div>
  )
}

function Detalle({ vehiculo }: { vehiculo: VehiculoPublico }) {
  const datos: [LucideIcon, string, string | null][] = [
    [CalendarDays, 'Año', String(vehiculo.anio)],
    [Gauge, 'Kilometraje', kilometros(vehiculo.kilometraje)],
    [Fuel, 'Combustible', etiqueta(vehiculo.combustible)],
    [Cog, 'Transmisión', etiqueta(vehiculo.transmision)],
    [Car, 'Carrocería', etiqueta(vehiculo.carroceria)],
    [Zap, 'Motor', vehiculo.motor],
    [DoorOpen, 'Puertas', vehiculo.puertas?.toString() ?? null],
    [Palette, 'Color', vehiculo.color],
  ]

  return (
    <>
      <Aparecer>
        <h2 className="text-2xl font-bold tracking-tight text-slate-950">Ficha técnica</h2>
      </Aparecer>

      <dl className="mt-6 grid grid-cols-2 gap-3 sm:grid-cols-3 xl:grid-cols-4">
        {datos
          .filter(([, , valor]) => valor)
          .map(([Icono, nombre, valor], indice) => (
            <Aparecer
              key={nombre}
              retraso={(indice % 4) * 0.05}
              className="rounded-2xl bg-slate-50 p-4 ring-1 ring-slate-900/5"
            >
              <dt className="flex items-center gap-2 text-xs font-medium text-slate-500">
                <Icono className="size-4" aria-hidden />
                {nombre}
              </dt>
              <dd className="mt-2 font-semibold text-slate-900">{valor}</dd>
            </Aparecer>
          ))}
      </dl>

      {vehiculo.descripcion && (
        <>
          <Aparecer>
            <h2 className="mt-12 text-2xl font-bold tracking-tight text-slate-950">Descripción</h2>
          </Aparecer>
          <p className="mt-4 whitespace-pre-line text-base leading-relaxed text-slate-600">
            {vehiculo.descripcion}
          </p>
        </>
      )}
    </>
  )
}

/**
 * Compartir la ficha. En el celular abre el menú del sistema, que ya trae WhatsApp; en la
 * compu, donde ese menú casi nunca existe, copia el link.
 */
function BotonCompartir({ titulo }: { titulo: string }) {
  const [copiado, setCopiado] = useState(false)

  async function compartir() {
    const url = window.location.href

    if (typeof navigator.share === 'function') {
      try {
        await navigator.share({ title: titulo, url })
      } catch {
        /* cerró el menú sin compartir */
      }
      return
    }

    try {
      await navigator.clipboard.writeText(url)
      setCopiado(true)
      setTimeout(() => setCopiado(false), 2000)
    } catch {
      /* sin permiso para el portapapeles: no hay nada útil que mostrar */
    }
  }

  return (
    <button
      type="button"
      onClick={() => void compartir()}
      className="inline-flex items-center justify-center gap-2 rounded-2xl px-6 py-3 text-sm font-semibold text-slate-600 transition hover:bg-slate-50 hover:text-slate-900"
    >
      {copiado ? (
        <>
          <Check className="size-4 text-emerald-600" aria-hidden />
          Link copiado
        </>
      ) : (
        <>
          <Share2 className="size-4" aria-hidden />
          Compartir
        </>
      )}
    </button>
  )
}

function EsqueletoDeFicha() {
  return (
    <div className="mx-auto max-w-7xl px-4 pt-8 sm:px-6">
      <Esqueleto className="h-5 w-64" />
      <div className="mt-5 grid gap-8 lg:grid-cols-[minmax(0,3fr)_minmax(0,2fr)] lg:gap-10">
        <Esqueleto className="aspect-4/3 rounded-3xl" />
        <Esqueleto className="h-112 rounded-3xl" />
      </div>
    </div>
  )
}

/** Crea o actualiza un meta tag del documento. */
function ponerMeta(nombre: string, contenido: string, esPropiedad = false) {
  const atributo = esPropiedad ? 'property' : 'name'
  let etiquetaMeta = document.head.querySelector<HTMLMetaElement>(`meta[${atributo}="${nombre}"]`)

  if (!etiquetaMeta) {
    etiquetaMeta = document.createElement('meta')
    etiquetaMeta.setAttribute(atributo, nombre)
    document.head.appendChild(etiquetaMeta)
  }

  etiquetaMeta.setAttribute('content', contenido)
}
