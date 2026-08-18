import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api, ApiError } from '@shared/api/client'
import { idDeVisita } from '@shared/analitica/sesion'
import { Esqueleto, Estado } from '@shared/ui/Estado'
import { kilometros, linkDeWhatsapp, precio } from '@shared/ui/formato'
import { colorPrimario, useSitio } from '@public/TenantContexto'
import type { VehiculoPublico } from '@shared/api/types'

export function FichaPage() {
  const { tenant, slug } = useSitio()
  const { id } = useParams<{ id: string }>()

  const [vehiculo, setVehiculo] = useState<VehiculoPublico | null>(null)
  const [error, setError] = useState<'no-esta' | 'falla' | null>(null)
  const [fotoActiva, setFotoActiva] = useState(0)

  const base = slug ? `/t/${slug}` : ''
  const primario = colorPrimario(tenant)
  const vehiculoId = Number(id)

  useEffect(() => {
    if (!Number.isFinite(vehiculoId)) {
      setError('no-esta')
      return
    }

    const controlador = new AbortController()
    setVehiculo(null)
    setError(null)
    setFotoActiva(0)

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
          className="rounded-lg px-4 py-2 font-semibold text-white"
          style={{ backgroundColor: primario }}
        >
          Ver vehículos
        </Link>
      </Estado>
    )
  }

  if (error === 'falla') {
    return <Estado titulo="No pudimos cargar el vehículo" detalle="Probá de nuevo en un momento." />
  }

  if (!vehiculo) {
    return (
      <div className="grid gap-6 lg:grid-cols-[3fr_2fr]">
        <Esqueleto className="aspect-4/3" />
        <Esqueleto className="h-64" />
      </div>
    )
  }

  const foto = vehiculo.fotos[fotoActiva] ?? vehiculo.fotos[0]

  function contactar(tipo: 'ClickWhatsapp' | 'ClickTelefono') {
    void api.publico.evento(slug, { tipo, vehiculoId, sessionId: idDeVisita() })
  }

  return (
    <article className="flex flex-col gap-6">
      <nav className="text-sm text-slate-500">
        <Link to={`${base}/vehiculos`} className="hover:underline">
          ← Volver al listado
        </Link>
      </nav>

      <div className="grid gap-6 lg:grid-cols-[3fr_2fr]">
        <section>
          <div className="overflow-hidden rounded-xl bg-slate-100">
            {foto ? (
              <img
                src={foto.url}
                alt={vehiculo.titulo}
                className="aspect-4/3 w-full object-cover"
              />
            ) : (
              <div className="grid aspect-4/3 place-items-center text-slate-400">Sin fotos</div>
            )}
          </div>

          {vehiculo.fotos.length > 1 && (
            <div className="mt-3 grid grid-cols-5 gap-2 sm:grid-cols-6">
              {vehiculo.fotos.map((f, indice) => (
                <button
                  key={f.id}
                  type="button"
                  onClick={() => setFotoActiva(indice)}
                  className={`overflow-hidden rounded-lg border-2 ${
                    indice === fotoActiva ? 'border-slate-900' : 'border-transparent'
                  }`}
                >
                  <img
                    src={f.urlThumb ?? f.url}
                    alt=""
                    loading="lazy"
                    className="aspect-4/3 w-full object-cover"
                  />
                </button>
              ))}
            </div>
          )}
        </section>

        <section className="flex flex-col gap-4">
          <div>
            <h1 className="text-2xl font-bold leading-tight">
              {vehiculo.marca} {vehiculo.modelo}
              {vehiculo.version && <span className="text-slate-500"> {vehiculo.version}</span>}
            </h1>
            <p className="mt-1 text-slate-500">
              {vehiculo.anio} · {kilometros(vehiculo.kilometraje)}
            </p>
            <p className="mt-3 text-3xl font-bold" style={{ color: primario }}>
              {precio(vehiculo.precio, vehiculo.moneda)}
            </p>
          </div>

          <div className="flex flex-col gap-2">
            {tenant.whatsapp && (
              <a
                href={linkDeWhatsapp(tenant.whatsapp, vehiculo.mensajeDeWhatsapp)}
                target="_blank"
                rel="noreferrer"
                onClick={() => contactar('ClickWhatsapp')}
                className="rounded-lg bg-emerald-600 px-4 py-3 text-center font-semibold text-white"
              >
                Consultar por WhatsApp
              </a>
            )}

            {tenant.telefono && (
              <a
                href={`tel:${tenant.telefono}`}
                onClick={() => contactar('ClickTelefono')}
                className="rounded-lg border border-slate-300 px-4 py-3 text-center font-semibold"
              >
                Llamar al {tenant.telefono}
              </a>
            )}
          </div>

          <dl className="divide-y divide-slate-200 rounded-xl border border-slate-200 bg-white text-sm">
            <Dato etiqueta="Carrocería" valor={vehiculo.carroceria} />
            <Dato etiqueta="Combustible" valor={vehiculo.combustible} />
            <Dato etiqueta="Transmisión" valor={vehiculo.transmision} />
            <Dato etiqueta="Color" valor={vehiculo.color} />
            <Dato etiqueta="Puertas" valor={vehiculo.puertas?.toString() ?? null} />
            <Dato etiqueta="Motor" valor={vehiculo.motor} />
          </dl>
        </section>
      </div>

      {vehiculo.descripcion && (
        <section className="rounded-xl border border-slate-200 bg-white p-5">
          <h2 className="mb-2 font-semibold">Descripción</h2>
          <p className="whitespace-pre-line text-slate-600">{vehiculo.descripcion}</p>
        </section>
      )}
    </article>
  )
}

function Dato({ etiqueta, valor }: { etiqueta: string; valor: string | null }) {
  if (!valor) return null

  return (
    <div className="flex items-center justify-between px-4 py-2.5">
      <dt className="text-slate-500">{etiqueta}</dt>
      <dd className="font-medium">{valor}</dd>
    </div>
  )
}

/** Crea o actualiza un meta tag del documento. */
function ponerMeta(nombre: string, contenido: string, esPropiedad = false) {
  const atributo = esPropiedad ? 'property' : 'name'
  let etiqueta = document.head.querySelector<HTMLMetaElement>(`meta[${atributo}="${nombre}"]`)

  if (!etiqueta) {
    etiqueta = document.createElement('meta')
    etiqueta.setAttribute(atributo, nombre)
    document.head.appendChild(etiqueta)
  }

  etiqueta.setAttribute('content', contenido)
}
