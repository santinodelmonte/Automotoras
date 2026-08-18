import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '@shared/api/client'
import { ComparativoDeMercado } from '@admin/ComparativoDeMercado'
import { Esqueleto, Estado } from '@shared/ui/Estado'
import { entero, fecha, precio } from '@shared/ui/formato'
import type {
  Benchmark,
  ReporteDeDemanda,
  SenalDeDemanda,
  SugerenciaDeCompra,
} from '@shared/api/types'

const VENTANAS = [30, 60, 90, 180]

const ETIQUETAS: Record<SenalDeDemanda, { texto: string; clase: string }> = {
  PrecioAlto: { texto: 'Revisar precio', clase: 'bg-amber-100 text-amber-900' },
  SinInteres: { texto: 'Nadie la mira', clase: 'bg-rose-100 text-rose-900' },
  Normal: { texto: 'En orden', clase: 'bg-emerald-100 text-emerald-900' },
  PocosDatos: { texto: 'Sin datos aún', clase: 'bg-slate-100 text-slate-600' },
}

/**
 * El reporte de demanda: por qué existe el producto.
 *
 * El catálogo lo tiene cualquiera. Lo que no tiene nadie es la respuesta a qué conviene
 * comprar, y sale de cruzar lo que la gente miró con lo que preguntó y con lo que buscó y
 * no estaba.
 */
export function ReportesPage() {
  const [dias, setDias] = useState(30)
  const [reporte, setReporte] = useState<ReporteDeDemanda | null>(null)
  const [sugerencias, setSugerencias] = useState<SugerenciaDeCompra[] | null>(null)
  const [benchmark, setBenchmark] = useState<Benchmark | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const controlador = new AbortController()
    setReporte(null)

    api.reportes
      .demanda(dias, controlador.signal)
      .then(setReporte)
      .catch((problema: unknown) => {
        if (controlador.signal.aborted) return
        setError(problema instanceof Error ? problema.message : 'No se pudo cargar el reporte.')
      })

    return () => controlador.abort()
  }, [dias])

  useEffect(() => {
    const controlador = new AbortController()
    setSugerencias(null)

    api.reportes
      .sugerencias(dias, controlador.signal)
      .then(setSugerencias)
      .catch(() => undefined)

    return () => controlador.abort()
  }, [dias])

  // El benchmark se pide aparte y su error se traga: depende de que haya mercado relevado,
  // y quedarse sin comparación no es motivo para tumbar el reporte propio.
  useEffect(() => {
    const controlador = new AbortController()
    setBenchmark(null)

    api.reportes
      .benchmark(dias, controlador.signal)
      .then(setBenchmark)
      .catch(() => undefined)

    return () => controlador.abort()
  }, [dias])

  if (error) return <Estado titulo="No pudimos cargar el reporte" detalle={error} />

  return (
    <div className="flex flex-col gap-8">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold">Demanda</h1>
          <p className="mt-1 text-sm text-slate-500">
            Qué se mira, qué se consulta y qué te están pidiendo que no tenés.
          </p>
        </div>

        <div className="flex gap-1 rounded-lg border border-slate-300 bg-white p-1">
          {VENTANAS.map((opcion) => (
            <button
              key={opcion}
              type="button"
              onClick={() => setDias(opcion)}
              className={`rounded px-3 py-1 text-sm ${
                dias === opcion ? 'bg-slate-900 text-white' : 'text-slate-600 hover:bg-slate-100'
              }`}
            >
              {opcion} días
            </button>
          ))}
        </div>
      </div>

      {!reporte ? (
        <Esqueleto className="h-96" />
      ) : (
        <>
          <section>
            <h2 className="mb-1 font-semibold">Qué conviene traer</h2>
            <p className="mb-4 text-sm text-slate-500">
              Lo que más te piden y no tenés, cruzado con lo rápido que vendés cosas
              parecidas. La demanda dice qué quieren; la rotación dice si conviene.
            </p>

            {sugerencias === null ? (
              <Esqueleto className="h-32" />
            ) : sugerencias.length === 0 ? (
              <p className="rounded-xl border border-slate-200 bg-white p-5 text-sm text-slate-400">
                Todavía no hay una demanda repetida como para sugerir una compra. Hacen falta
                al menos tres búsquedas de lo mismo sin resultado.
              </p>
            ) : (
              <ol className="flex flex-col gap-2">
                {sugerencias.map((sugerencia, indice) => (
                  <li
                    key={`${sugerencia.descripcion}-${indice}`}
                    className="rounded-xl border border-slate-200 bg-white p-4"
                  >
                    <div className="flex items-start justify-between gap-4">
                      <div className="min-w-0">
                        <p className="font-semibold">{sugerencia.descripcion}</p>
                        <p className="mt-1 text-sm text-slate-600">{sugerencia.fundamento}</p>
                      </div>

                      {sugerencia.diasPromedioParaVender !== null && (
                        <div className="shrink-0 text-right">
                          <p className="text-2xl font-bold text-emerald-700">
                            {sugerencia.diasPromedioParaVender}
                          </p>
                          <p className="text-xs uppercase tracking-wide text-slate-400">
                            días para vender
                          </p>
                        </div>
                      )}
                    </div>
                  </li>
                ))}
              </ol>
            )}
          </section>

          <section>
            <h2 className="mb-1 font-semibold">Qué te están pidiendo y no tenés</h2>
            <p className="mb-4 text-sm text-slate-500">
              Búsquedas que no devolvieron ningún resultado, agrupadas por lo que se pidió. Es
              lo más parecido a una lista de compras escrita por los propios compradores.
            </p>

            {reporte.demandaInsatisfecha.length === 0 ? (
              <p className="rounded-xl border border-slate-200 bg-white p-5 text-sm text-slate-400">
                En este período nadie buscó algo que no estuviera. Con más tráfico esto se
                empieza a poblar solo.
              </p>
            ) : (
              <ul className="divide-y divide-slate-100 rounded-xl border border-slate-200 bg-white">
                {reporte.demandaInsatisfecha.map((pedido, indice) => (
                  <li
                    key={`${pedido.descripcion}-${indice}`}
                    className="flex items-center justify-between gap-4 px-5 py-3"
                  >
                    <div className="min-w-0">
                      <p className="font-medium">{pedido.descripcion}</p>
                      <p className="text-sm text-slate-500">Última vez: {fecha(pedido.ultimaVez)}</p>
                    </div>
                    <span className="shrink-0 rounded-full bg-slate-900 px-3 py-1 text-sm font-semibold text-white">
                      {entero(pedido.veces)} {pedido.veces === 1 ? 'búsqueda' : 'búsquedas'}
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </section>

          {benchmark && <ContraElMercado benchmark={benchmark} />}

          <section>
            <h2 className="mb-1 font-semibold">Tu stock publicado</h2>
            <p className="mb-4 text-sm text-slate-500">
              {entero(reporte.vistasTotales)} visitas y {entero(reporte.consultasTotales)}{' '}
              consultas en {reporte.diasAnalizados} días. Lo que necesita una decisión va primero.
            </p>

            {reporte.vehiculos.length === 0 ? (
              <p className="rounded-xl border border-slate-200 bg-white p-5 text-sm text-slate-400">
                No hay vehículos publicados.
              </p>
            ) : (
              <ul className="flex flex-col gap-2">
                {reporte.vehiculos.map((vehiculo) => {
                  const etiqueta = ETIQUETAS[vehiculo.senal]

                  return (
                    <li
                      key={vehiculo.vehiculoId}
                      className="rounded-xl border border-slate-200 bg-white p-4"
                    >
                      <div className="flex items-start gap-4">
                        <div className="h-16 w-20 shrink-0 overflow-hidden rounded-lg bg-slate-100">
                          {vehiculo.fotoPortadaUrl && (
                            <img
                              src={vehiculo.fotoPortadaUrl}
                              alt=""
                              loading="lazy"
                              className="h-full w-full object-cover"
                            />
                          )}
                        </div>

                        <div className="min-w-0 flex-1">
                          <div className="flex flex-wrap items-center gap-2">
                            <Link
                              to={`/admin/vehiculos/${vehiculo.vehiculoId}`}
                              className="font-semibold hover:underline"
                            >
                              {vehiculo.marca} {vehiculo.modelo} {vehiculo.anio}
                            </Link>
                            <span
                              className={`rounded-full px-2 py-0.5 text-xs font-semibold ${etiqueta.clase}`}
                            >
                              {etiqueta.texto}
                            </span>
                          </div>

                          <p className="mt-1 text-sm text-slate-600">{vehiculo.lectura}</p>

                          <dl className="mt-2 flex flex-wrap gap-x-6 gap-y-1 text-sm text-slate-500">
                            <Metrica etiqueta="Precio" valor={precio(vehiculo.precio, vehiculo.moneda)} />
                            <Metrica etiqueta="En góndola" valor={`${vehiculo.diasEnGondola} días`} />
                            <Metrica etiqueta="Vistas" valor={entero(vehiculo.vistas)} />
                            <Metrica etiqueta="Consultas" valor={entero(vehiculo.consultas)} />
                            <Metrica
                              etiqueta="Consultas c/100 vistas"
                              valor={vehiculo.consultasPorCienVistas.toFixed(1)}
                            />
                            {vehiculo.precioDeMercado !== null && (
                              <Metrica
                                etiqueta="Mercado"
                                valor={precio(vehiculo.precioDeMercado, vehiculo.moneda)}
                                acento={
                                  vehiculo.diferenciaConElMercado !== null &&
                                  vehiculo.diferenciaConElMercado > 0
                                    ? 'text-amber-700'
                                    : 'text-emerald-700'
                                }
                              />
                            )}
                          </dl>
                        </div>
                      </div>
                    </li>
                  )
                })}
              </ul>
            )}
          </section>
        </>
      )}
    </div>
  )
}

/**
 * Cómo le va a esta automotora contra el resto, sin que ninguna otra sea identificable.
 *
 * Si no hay ninguna comparación no se dibuja nada. La sección vacía invitaría a leer el
 * silencio como un mal resultado, cuando lo único que dice es que todavía no hay suficiente
 * mercado relevado.
 */
function ContraElMercado({ benchmark }: { benchmark: Benchmark }) {
  const hayAlgo =
    benchmark.consultasPorCienVistas !== null ||
    benchmark.diasParaVenderPorCarroceria.length > 0

  if (!hayAlgo) return null

  return (
    <section>
      <h2 className="mb-1 font-semibold">Cómo te va contra el mercado</h2>
      <p className="mb-4 text-sm text-slate-500">{benchmark.notaDePrivacidad}</p>

      {benchmark.consultasPorCienVistas && (
        <ul className="flex flex-col gap-2">
          <ComparativoDeMercado
            comparativo={benchmark.consultasPorCienVistas}
            direccion="masEsMejor"
            unidad="c/100"
          />
        </ul>
      )}

      {benchmark.diasParaVenderPorCarroceria.length > 0 && (
        <>
          <h3 className="mb-2 mt-6 text-sm font-semibold uppercase tracking-wide text-slate-400">
            Días para vender, por carrocería
          </h3>

          <ul className="flex flex-col gap-2">
            {benchmark.diasParaVenderPorCarroceria.map((comparativo) => (
              <ComparativoDeMercado
                key={comparativo.dimension}
                comparativo={comparativo}
                direccion="menosEsMejor"
                unidad="días"
              />
            ))}
          </ul>
        </>
      )}
    </section>
  )
}

function Metrica({
  etiqueta,
  valor,
  acento,
}: {
  etiqueta: string
  valor: string
  acento?: string
}) {
  return (
    <div>
      <dt className="text-xs uppercase tracking-wide text-slate-400">{etiqueta}</dt>
      <dd className={`font-medium ${acento ?? 'text-slate-700'}`}>{valor}</dd>
    </div>
  )
}
