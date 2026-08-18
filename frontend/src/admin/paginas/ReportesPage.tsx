import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { api } from '@shared/api/client'
import { Esqueleto, Estado } from '@shared/ui/Estado'
import { Tarjeta } from '@shared/ui/Tarjeta'
import { entero, fecha, precio } from '@shared/ui/formato'
import type {
  Benchmark,
  BusquedaSinResultado,
  DemandaDeVehiculo,
  MetricaComparada,
  ReporteDeDemanda,
  SenalDeDemanda,
  SugerenciaDeCompra,
} from '@shared/api/types'

/**
 * Los reportes de demanda: qué se mira, qué se consulta, qué se busca y no está, y qué
 * conviene comprar.
 *
 * Las sugerencias van arriba de todo y el detalle abajo. El orden no es estético: el dueño
 * entra con una pregunta —qué compro— y la tabla de vistas por unidad es la evidencia que
 * la sostiene, no la respuesta.
 */
export function ReportesPage() {
  const [parametros, setParametros] = useSearchParams()
  const dias = Number(parametros.get('dias') ?? '90')

  const [reporte, setReporte] = useState<ReporteDeDemanda | null>(null)
  const [sugerencias, setSugerencias] = useState<SugerenciaDeCompra[] | null>(null)
  const [busquedas, setBusquedas] = useState<BusquedaSinResultado[] | null>(null)
  const [benchmark, setBenchmark] = useState<Benchmark | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const controlador = new AbortController()

    setReporte(null)
    setSugerencias(null)
    setBusquedas(null)
    setBenchmark(null)
    setError(null)

    Promise.all([
      api.reportes.demanda(dias, controlador.signal),
      api.reportes.sugerencias(dias, controlador.signal),
      api.reportes.busquedasSinResultado(dias, controlador.signal),
      api.reportes.benchmark(dias, controlador.signal),
    ])
      .then(([demanda, compras, vacias, comparacion]) => {
        setReporte(demanda)
        setSugerencias(compras)
        setBusquedas(vacias)
        setBenchmark(comparacion)
      })
      .catch((problema: unknown) => {
        if (controlador.signal.aborted) return
        setError(problema instanceof Error ? problema.message : 'No se pudieron cargar los reportes.')
      })

    return () => controlador.abort()
  }, [dias])

  return (
    <div className="flex flex-col gap-8">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold">Demanda</h1>
          <p className="text-sm text-slate-500">
            Qué miran, qué consultan y qué buscan sin encontrar en tu sitio.
          </p>
        </div>

        <select
          value={String(dias)}
          onChange={(e) => {
            const siguientes = new URLSearchParams(parametros)
            siguientes.set('dias', e.target.value)
            setParametros(siguientes, { replace: true })
          }}
          className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm"
        >
          <option value="30">Últimos 30 días</option>
          <option value="90">Últimos 90 días</option>
          <option value="365">Último año</option>
        </select>
      </div>

      {error && <Estado titulo="No pudimos cargar los reportes" detalle={error} />}

      {!error && !reporte && (
        <div className="flex flex-col gap-4">
          <Esqueleto className="h-24" />
          <Esqueleto className="h-40" />
          <Esqueleto className="h-64" />
        </div>
      )}

      {reporte && (
        <>
          <Resumen reporte={reporte} />
          {benchmark && <Comparativa benchmark={benchmark} />}
          <Sugerencias sugerencias={sugerencias ?? []} />
          <PorVehiculo reporte={reporte} />
          <SinResultado busquedas={busquedas ?? []} />
        </>
      )}
    </div>
  )
}

function Resumen({ reporte }: { reporte: ReporteDeDemanda }) {
  const { resumen } = reporte

  return (
    <section className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
      <Tarjeta titulo="Vistas de ficha" valor={entero(resumen.vistas)} />
      <Tarjeta
        titulo="Consultas"
        valor={entero(resumen.consultas)}
        nota={`${resumen.consultasPorCienVistas.toLocaleString('es-UY')} cada 100 vistas`}
      />
      <Tarjeta
        titulo="Días en góndola"
        valor={entero(resumen.diasEnGondolaMediana)}
        nota={`Mediana. Promedio: ${entero(resumen.diasEnGondolaPromedio)}`}
      />
      <Tarjeta
        titulo="Vendidos"
        valor={entero(resumen.vendidosEnElPeriodo)}
        nota={
          resumen.diasHastaLaVentaPromedio === null
            ? 'Sin ventas en el período'
            : `${entero(resumen.diasHastaLaVentaPromedio)} días hasta vender, promedio`
        }
      />
    </section>
  )
}

/**
 * Cómo le va comparada con el resto del mercado del SaaS.
 *
 * Cuando la muestra es chica no se muestra un número aproximado: se muestra por qué no
 * hay. Un agregado de dos competidores no es un agregado, y el producto se apoya en que
 * ninguna automotora pueda deducir nada de otra.
 */
function Comparativa({ benchmark }: { benchmark: Benchmark }) {
  return (
    <section className="rounded-xl border border-slate-200 bg-white p-5">
      <h2 className="font-semibold">Contra el resto del mercado</h2>

      {!benchmark.disponible ? (
        <p className="mt-3 text-sm text-slate-400">{benchmark.motivo}</p>
      ) : (
        <>
          <p className="mt-1 text-sm text-slate-500">
            Mediana entre {entero(benchmark.automotorasEnLaMuestra)} automotoras, sin datos de
            ninguna en particular.
          </p>

          <div className="mt-4 grid gap-4 sm:grid-cols-3">
            <Comparacion titulo="Días en góndola" metrica={benchmark.diasEnGondola} />
            <Comparacion titulo="Consultas cada 100 vistas" metrica={benchmark.consultasPorCienVistas} />
            <Comparacion titulo="Días hasta vender" metrica={benchmark.diasHastaLaVenta} />
          </div>
        </>
      )}
    </section>
  )
}

function Comparacion({ titulo, metrica }: { titulo: string; metrica: MetricaComparada | null }) {
  if (!metrica || metrica.propio === null || metrica.mercado === null) {
    return (
      <div className="rounded-lg border border-slate-200 p-4">
        <p className="text-sm text-slate-500">{titulo}</p>
        <p className="mt-2 text-xs text-slate-400">Sin muestra suficiente para comparar.</p>
      </div>
    )
  }

  const mejor = metrica.mejorCuandoBaja ? metrica.propio < metrica.mercado : metrica.propio > metrica.mercado

  return (
    <div className="rounded-lg border border-slate-200 p-4">
      <p className="text-sm text-slate-500">{titulo}</p>

      <p className={`mt-1 text-3xl font-bold ${mejor ? 'text-emerald-700' : 'text-slate-900'}`}>
        {metrica.propio.toLocaleString('es-UY')}
      </p>

      <p className="mt-1 text-xs text-slate-400">
        Mercado: {metrica.mercado.toLocaleString('es-UY')}
      </p>
    </div>
  )
}

/**
 * Lo que hay que hacer, arriba. Cada fila lleva la evidencia al lado: cuántas visitas
 * distintas lo pidieron y cuántas unidades hay en el patio.
 */
function Sugerencias({ sugerencias }: { sugerencias: SugerenciaDeCompra[] }) {
  return (
    <section className="rounded-xl border border-slate-200 bg-white p-5">
      <h2 className="font-semibold">Qué conviene comprar</h2>
      <p className="mt-1 text-sm text-slate-500">
        Sale de las búsquedas que no encontraron nada, cruzadas contra tu stock publicado.
      </p>

      {sugerencias.length === 0 && (
        <p className="mt-4 text-sm text-slate-400">
          Todavía no hay demanda insatisfecha suficiente para sugerir una compra. Hacen falta
          al menos tres visitas distintas buscando lo mismo.
        </p>
      )}

      <ul className="mt-4 flex flex-col gap-3">
        {sugerencias.map((sugerencia, indice) => (
          <li
            key={`${sugerencia.modeloId ?? sugerencia.marcaId ?? sugerencia.carroceria}-${indice}`}
            className="rounded-lg border border-slate-200 p-4"
          >
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <p className="font-semibold">{queSeBuscaba(sugerencia)}</p>
                <p className="mt-1 text-sm text-slate-500">
                  {sugerencia.visitas === 1 ? '1 visita' : `${entero(sugerencia.visitas)} visitas`} lo
                  buscaron y no encontraron nada
                  {sugerencia.presupuestoTipico !== null && sugerencia.moneda
                    ? `, con un tope habitual de ${precio(sugerencia.presupuestoTipico, sugerencia.moneda)}`
                    : ''}
                  .
                </p>
              </div>

              <span
                className={`rounded-full px-3 py-1 text-xs font-semibold ${
                  sugerencia.tipo === 'Comprar'
                    ? 'bg-emerald-100 text-emerald-800'
                    : 'bg-amber-100 text-amber-800'
                }`}
              >
                {sugerencia.tipo === 'Comprar' ? 'Comprar' : 'Revisar lo que tenés'}
              </span>
            </div>

            <p className="mt-2 text-sm text-slate-600">
              {sugerencia.tipo === 'Comprar'
                ? 'No tenés ninguna unidad publicada de eso.'
                : `Tenés ${entero(sugerencia.unidadesEnStock)} ${
                    sugerencia.unidadesEnStock === 1 ? 'unidad publicada' : 'unidades publicadas'
                  } y ninguna entró en lo que pedían: mirá precio, año o kilometraje.`}
            </p>
          </li>
        ))}
      </ul>
    </section>
  )
}

const ETIQUETAS: Record<SenalDeDemanda, { texto: string; clase: string }> = {
  SinDatos: { texto: 'Sin datos', clase: 'bg-slate-100 text-slate-500' },
  Saludable: { texto: 'Saludable', clase: 'bg-emerald-100 text-emerald-800' },
  PrecioAlto: { texto: 'Precio alto', clase: 'bg-amber-100 text-amber-800' },
  SinVisibilidad: { texto: 'Sin visibilidad', clase: 'bg-sky-100 text-sky-800' },
  Estancado: { texto: 'Estancado', clase: 'bg-rose-100 text-rose-800' },
}

/** La evidencia, unidad por unidad. */
function PorVehiculo({ reporte }: { reporte: ReporteDeDemanda }) {
  if (reporte.vehiculos.length === 0) {
    return (
      <Estado
        titulo="No hay vehículos publicados"
        detalle="El reporte mira lo que está disponible o reservado: lo pausado y lo vendido no se puede ver desde el sitio."
      />
    )
  }

  return (
    <section className="rounded-xl border border-slate-200 bg-white p-5">
      <h2 className="font-semibold">Unidad por unidad</h2>
      <p className="mt-1 text-sm text-slate-500">
        Muchas vistas con pocas consultas apunta al precio. Pocas vistas apunta a las fotos, al
        título o a que el aviso no aparece.
      </p>

      <div className="mt-4 overflow-x-auto">
        <table className="w-full min-w-[640px] text-sm">
          <thead className="text-left text-xs uppercase tracking-wide text-slate-400">
            <tr>
              <th className="pb-2 font-medium">Vehículo</th>
              <th className="pb-2 text-right font-medium">Góndola</th>
              <th className="pb-2 text-right font-medium">Vistas</th>
              <th className="pb-2 text-right font-medium">Consultas</th>
              <th className="pb-2 text-right font-medium">Cada 100</th>
              <th className="pb-2 text-right font-medium">vs. mercado</th>
              <th className="pb-2 text-right font-medium">Señal</th>
            </tr>
          </thead>

          <tbody className="divide-y divide-slate-100">
            {reporte.vehiculos.map((fila) => (
              <tr key={fila.vehiculoId}>
                <td className="py-3">
                  <Link
                    to={`/admin/vehiculos/${fila.vehiculoId}`}
                    className="font-medium hover:underline"
                  >
                    {fila.marca} {fila.modelo} {fila.anio}
                  </Link>
                  <p className="text-xs text-slate-400">{precio(fila.precio, fila.moneda)}</p>
                </td>
                <td className="py-3 text-right text-slate-600">{entero(fila.diasEnGondola)} d</td>
                <td className="py-3 text-right text-slate-600">{entero(fila.vistas)}</td>
                <td className="py-3 text-right text-slate-600">{entero(fila.consultas)}</td>
                <td className="py-3 text-right text-slate-600">
                  {fila.consultasPorCienVistas.toLocaleString('es-UY')}
                </td>
                <td className="py-3 text-right">
                  <ContraElMercado fila={fila} />
                </td>
                <td className="py-3 text-right">
                  <span
                    className={`rounded-full px-2.5 py-1 text-xs font-semibold ${ETIQUETAS[fila.senal].clase}`}
                  >
                    {ETIQUETAS[fila.senal].texto}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  )
}

/**
 * Cuánto se aparta el precio publicado del de mercado.
 *
 * Mientras no haya snapshot dice "sin referencia" y no un cero: un cero se lee como que el
 * mercado lo regala. Cuando lo hay, la fecha va en el `title` — comparar contra un número
 * de hace tres meses y no saberlo es peor que no comparar.
 */
function ContraElMercado({ fila }: { fila: DemandaDeVehiculo }) {
  if (fila.diferenciaConElMercado === null || fila.precioDeMercado === null) {
    return <span className="text-xs text-slate-400">Sin referencia</span>
  }

  const arriba = fila.diferenciaConElMercado > 0

  return (
    <span
      title={`Mercado: ${precio(fila.precioDeMercado, fila.moneda)} al ${fecha(fila.precioDeMercadoAl)}`}
      className={arriba ? 'font-semibold text-rose-700' : 'font-semibold text-emerald-700'}
    >
      {arriba ? '+' : ''}
      {fila.diferenciaConElMercado.toLocaleString('es-UY')}%
    </span>
  )
}

/**
 * Las búsquedas vacías completas, incluidas las que no nombran un modelo y por eso no
 * llegan a ser una sugerencia. Se muestran igual: un dueño que ve diez búsquedas de hasta
 * diez mil dólares aprende algo sobre su lista de precios.
 */
function SinResultado({ busquedas }: { busquedas: BusquedaSinResultado[] }) {
  return (
    <section className="rounded-xl border border-slate-200 bg-white p-5">
      <h2 className="font-semibold">Búsquedas sin resultado</h2>
      <p className="mt-1 text-sm text-slate-500">
        Cada fila es gente que entró, buscó y se fue con las manos vacías.
      </p>

      {busquedas.length === 0 && (
        <p className="mt-4 text-sm text-slate-400">
          Todas las búsquedas del período encontraron algo.
        </p>
      )}

      <ul className="mt-4 divide-y divide-slate-100">
        {busquedas.map((busqueda, indice) => (
          <li
            key={`${busqueda.modeloId ?? busqueda.marcaId ?? busqueda.carroceria ?? 'libre'}-${indice}`}
            className="flex flex-wrap items-center justify-between gap-2 py-3"
          >
            <div>
              <p className="font-medium">{queSeBuscaba(busqueda)}</p>
              <p className="text-xs text-slate-400">Última vez: {fecha(busqueda.ultimaVez)}</p>
            </div>

            <div className="text-right text-sm">
              <p className="font-semibold">
                {entero(busqueda.sesiones)} {busqueda.sesiones === 1 ? 'visita' : 'visitas'}
              </p>
              <p className="text-slate-500">
                {entero(busqueda.veces)} {busqueda.veces === 1 ? 'búsqueda' : 'búsquedas'}
              </p>
            </div>
          </li>
        ))}
      </ul>
    </section>
  )
}

/**
 * Arma la frase de qué se estaba buscando con lo que haya: modelo si lo nombraron, si no
 * la marca, si no la carrocería, y si no había nada de eso, el rango de precio.
 */
function queSeBuscaba(grupo: {
  marca: string | null
  modelo: string | null
  carroceria: string | null
  anioDesde: number | null
  anioHasta: number | null
  moneda: string | null
  presupuestoTipico?: number | null
  precioHasta?: number | null
}): string {
  const partes: string[] = []

  if (grupo.modelo) partes.push(grupo.marca ? `${grupo.marca} ${grupo.modelo}` : grupo.modelo)
  else if (grupo.marca) partes.push(grupo.marca)
  else if (grupo.carroceria) partes.push(grupo.carroceria)

  if (grupo.anioDesde && grupo.anioHasta) partes.push(`${grupo.anioDesde}–${grupo.anioHasta}`)
  else if (grupo.anioDesde) partes.push(`${grupo.anioDesde} en adelante`)
  else if (grupo.anioHasta) partes.push(`hasta ${grupo.anioHasta}`)

  if (partes.length === 0) {
    const tope = grupo.presupuestoTipico ?? grupo.precioHasta

    return tope && grupo.moneda
      ? `Cualquier vehículo de hasta ${precio(tope, grupo.moneda)}`
      : 'Búsqueda sin marca ni modelo'
  }

  return partes.join(' · ')
}
