import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { ArrowUpRight, Car, Clock, Eye, MessageCircle, Plus, SearchX } from 'lucide-react'
import { api } from '@shared/api/client'
import { useSesion } from '@shared/auth/useSesion'
import { Esqueleto, Estado } from '@shared/ui/Estado'
import { Tarjeta } from '@shared/ui/Tarjeta'
import { entero } from '@shared/ui/formato'
import type { Dashboard, EstadoVehiculo } from '@shared/api/types'
import { Pagina, Seccion } from '@admin/ui/Pagina'

const COLOR_DE_ESTADO: Record<EstadoVehiculo, string> = {
  Disponible: 'bg-emerald-500',
  Reservado: 'bg-amber-400',
  Vendido: 'bg-slate-400',
  Pausado: 'bg-slate-200',
}

export function DashboardPage() {
  const [tablero, setTablero] = useState<Dashboard | null>(null)
  const [error, setError] = useState<string | null>(null)
  const sesion = useSesion()

  useEffect(() => {
    const controlador = new AbortController()

    api
      .dashboard(controlador.signal)
      .then(setTablero)
      .catch((problema: unknown) => {
        if (controlador.signal.aborted) return
        setError(problema instanceof Error ? problema.message : 'No se pudo cargar el tablero.')
      })

    return () => controlador.abort()
  }, [])

  const nombre = sesion?.usuario.nombre.split(' ')[0]

  const encabezado = {
    titulo: nombre ? `Hola, ${nombre}` : 'Tablero',
    descripcion: 'Cómo viene tu stock y qué está pasando en tu sitio en los últimos 30 días.',
    acciones: (
      <Link to="/admin/vehiculos/nuevo" className="panel-boton">
        <Plus aria-hidden className="size-4" />
        Cargar vehículo
      </Link>
    ),
  }

  if (error) return <Estado titulo="No pudimos cargar el tablero" detalle={error} />

  if (!tablero) {
    return (
      <Pagina {...encabezado}>
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {[0, 1, 2, 3].map((i) => (
            <Esqueleto key={i} className="h-28 rounded-2xl" />
          ))}
        </div>
        <Esqueleto className="h-64 rounded-2xl" />
      </Pagina>
    )
  }

  const totalPorEstado = tablero.vehiculosPorEstado.reduce((suma, c) => suma + c.cantidad, 0)
  const maximoDeVistas = Math.max(1, ...tablero.masVistos.map((v) => v.vistas))

  return (
    <Pagina {...encabezado}>
      <section className="grid grid-cols-2 gap-3 sm:gap-4 lg:grid-cols-4">
        <Tarjeta titulo="Vehículos" valor={entero(tablero.totalDeVehiculos)} nota="En el stock, de todos los estados" icono={Car} />
        <Tarjeta
          titulo="Vistas"
          valor={entero(tablero.vistasUltimos30Dias)}
          nota="Fichas abiertas en tu sitio"
          icono={Eye}
          tono="azul"
        />
        <Tarjeta
          titulo="Consultas"
          valor={entero(tablero.consultasUltimos30Dias)}
          nota="WhatsApp y teléfono"
          icono={MessageCircle}
          tono="violeta"
        />
        <Tarjeta
          titulo="Días en góndola"
          valor={entero(tablero.diasEnGondolaPromedio)}
          nota="Promedio de lo publicado"
          icono={Clock}
          tono="ambar"
        />
      </section>

      <section className="grid gap-4 lg:grid-cols-5">
        <Seccion titulo="Stock por estado" className="lg:col-span-3">
          {totalPorEstado === 0 ? (
            <p className="panel-ayuda">Todavía no hay vehículos cargados.</p>
          ) : (
            <>
              <div className="flex h-2.5 gap-0.5 overflow-hidden rounded-full bg-slate-100">
                {tablero.vehiculosPorEstado.map((conteo) => (
                  <div
                    key={conteo.estado}
                    className={COLOR_DE_ESTADO[conteo.estado]}
                    style={{ width: `${(conteo.cantidad / totalPorEstado) * 100}%` }}
                  />
                ))}
              </div>

              <ul className="mt-5 grid gap-3 sm:grid-cols-2">
                {tablero.vehiculosPorEstado.map((conteo) => (
                  <li key={conteo.estado}>
                    <Link
                      to={`/admin/vehiculos?estado=${conteo.estado}`}
                      className="flex items-center justify-between rounded-xl border border-slate-200 px-4 py-3 transition hover:border-slate-300 hover:bg-slate-50"
                    >
                      <span className="flex items-center gap-2.5 text-sm text-slate-600">
                        <span aria-hidden className={`size-2.5 rounded-full ${COLOR_DE_ESTADO[conteo.estado]}`} />
                        {conteo.estado}
                      </span>
                      <span className="text-lg font-semibold tabular-nums">{entero(conteo.cantidad)}</span>
                    </Link>
                  </li>
                ))}
              </ul>
            </>
          )}
        </Seccion>

        <Link
          to="/admin/reportes"
          className="group panel-seccion border-rose-100 bg-linear-to-br from-rose-50 via-white to-white transition hover:border-rose-200 hover:shadow-tarjeta lg:col-span-2"
        >
          <span aria-hidden className="grid size-10 place-items-center rounded-xl bg-rose-100 text-rose-600">
            <SearchX className="size-5" />
          </span>
          <p className="mt-4 text-sm font-medium text-slate-500">Búsquedas sin resultado</p>
          <p className="mt-1 text-4xl font-semibold tracking-tight tabular-nums">
            {entero(tablero.busquedasSinResultadoUltimos30Dias)}
          </p>
          <p className="mt-2 text-sm text-slate-600">
            Gente que buscó algo en tu sitio y no lo encontró. Es la señal más directa de qué
            stock te están pidiendo.
          </p>
          <span className="mt-4 inline-flex items-center gap-1 text-sm font-semibold text-rose-700">
            Ver qué te piden
            <ArrowUpRight
              aria-hidden
              className="size-4 transition group-hover:-translate-y-0.5 group-hover:translate-x-0.5"
            />
          </span>
        </Link>
      </section>

      <Seccion
        titulo="Lo más visto"
        descripcion="Muchas vistas con pocas consultas suele querer decir que el precio está alto."
        acciones={
          <Link to="/admin/reportes" className="panel-boton-secundario">
            Ver el reporte completo
          </Link>
        }
      >
        {tablero.masVistos.length === 0 ? (
          <p className="panel-ayuda">
            Todavía no hay visitas registradas. Los datos aparecen a medida que el sitio recibe
            tráfico.
          </p>
        ) : (
          <ul className="-mx-2 flex flex-col">
            {tablero.masVistos.map((vehiculo) => (
              <li key={vehiculo.vehiculoId}>
                <Link
                  to={`/admin/vehiculos/${vehiculo.vehiculoId}`}
                  className="flex items-center gap-3 rounded-xl px-2 py-2.5 transition hover:bg-slate-50 sm:gap-4"
                >
                  <div className="h-10 w-14 shrink-0 sm:h-12 sm:w-16 overflow-hidden rounded-lg bg-slate-100">
                    {vehiculo.fotoPortadaUrl && (
                      <img src={vehiculo.fotoPortadaUrl} alt="" loading="lazy" className="h-full w-full object-cover" />
                    )}
                  </div>

                  <div className="min-w-0 flex-1">
                    <p className="font-medium text-slate-900 sm:truncate">
                      {vehiculo.marca} {vehiculo.modelo}{' '}
                      <span className="font-normal text-slate-400">{vehiculo.anio}</span>
                    </p>
                    <div className="mt-1.5 h-1.5 max-w-64 overflow-hidden rounded-full bg-slate-100">
                      <div
                        className="h-full rounded-full bg-sky-400"
                        style={{ width: `${(vehiculo.vistas / maximoDeVistas) * 100}%` }}
                      />
                    </div>
                  </div>

                  <div className="shrink-0 text-right text-sm tabular-nums">
                    <p className="font-semibold text-slate-900">{entero(vehiculo.vistas)} vistas</p>
                    <p className="text-slate-500">
                      {entero(vehiculo.consultas)} {vehiculo.consultas === 1 ? 'consulta' : 'consultas'}
                    </p>
                  </div>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </Seccion>
    </Pagina>
  )
}
