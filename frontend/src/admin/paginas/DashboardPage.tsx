import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '@shared/api/client'
import { Esqueleto, Estado } from '@shared/ui/Estado'
import { entero } from '@shared/ui/formato'
import type { Dashboard } from '@shared/api/types'

export function DashboardPage() {
  const [tablero, setTablero] = useState<Dashboard | null>(null)
  const [error, setError] = useState<string | null>(null)

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

  if (error) return <Estado titulo="No pudimos cargar el tablero" detalle={error} />

  if (!tablero) {
    return (
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {[0, 1, 2, 3].map((i) => (
          <Esqueleto key={i} className="h-24" />
        ))}
      </div>
    )
  }

  return (
    <div className="flex flex-col gap-8">
      <section className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <Tarjeta titulo="Vehículos" valor={entero(tablero.totalDeVehiculos)} />
        <Tarjeta
          titulo="Vistas (30 días)"
          valor={entero(tablero.vistasUltimos30Dias)}
        />
        <Tarjeta
          titulo="Consultas (30 días)"
          valor={entero(tablero.consultasUltimos30Dias)}
          nota="WhatsApp y teléfono"
        />
        <Tarjeta
          titulo="Días en góndola"
          valor={entero(tablero.diasEnGondolaPromedio)}
          nota="Promedio de lo publicado"
        />
      </section>

      <section className="grid gap-6 lg:grid-cols-2">
        <div className="rounded-xl border border-slate-200 bg-white p-5">
          <h2 className="font-semibold">Stock por estado</h2>
          <ul className="mt-3 divide-y divide-slate-100">
            {tablero.vehiculosPorEstado.map((conteo) => (
              <li key={conteo.estado} className="flex items-center justify-between py-2">
                <span className="text-slate-600">{conteo.estado}</span>
                <span className="font-semibold">{entero(conteo.cantidad)}</span>
              </li>
            ))}
            {tablero.vehiculosPorEstado.length === 0 && (
              <li className="py-2 text-sm text-slate-400">Todavía no hay vehículos cargados.</li>
            )}
          </ul>
        </div>

        <div className="rounded-xl border border-slate-200 bg-white p-5">
          <h2 className="font-semibold">Búsquedas sin resultado</h2>
          <p className="mt-3 text-3xl font-bold">
            {entero(tablero.busquedasSinResultadoUltimos30Dias)}
          </p>
          <p className="mt-2 text-sm text-slate-500">
            En los últimos 30 días alguien buscó eso en tu sitio y no encontró nada. Es la
            señal más directa de qué stock te están pidiendo.
          </p>
        </div>
      </section>

      <section className="rounded-xl border border-slate-200 bg-white p-5">
        <h2 className="font-semibold">Más vistos (30 días)</h2>
        <p className="mt-1 text-sm text-slate-500">
          Muchas vistas con pocas consultas suele querer decir que el precio está alto.
        </p>

        <ul className="mt-4 divide-y divide-slate-100">
          {tablero.masVistos.map((vehiculo) => (
            <li key={vehiculo.vehiculoId} className="flex items-center gap-3 py-3">
              <div className="h-12 w-16 shrink-0 overflow-hidden rounded-lg bg-slate-100">
                {vehiculo.fotoPortadaUrl && (
                  <img
                    src={vehiculo.fotoPortadaUrl}
                    alt=""
                    loading="lazy"
                    className="h-full w-full object-cover"
                  />
                )}
              </div>

              <Link
                to={`/admin/vehiculos/${vehiculo.vehiculoId}`}
                className="flex-1 font-medium hover:underline"
              >
                {vehiculo.marca} {vehiculo.modelo} {vehiculo.anio}
              </Link>

              <div className="text-right text-sm">
                <p className="font-semibold">{entero(vehiculo.vistas)} vistas</p>
                <p className="text-slate-500">{entero(vehiculo.consultas)} consultas</p>
              </div>
            </li>
          ))}

          {tablero.masVistos.length === 0 && (
            <li className="py-3 text-sm text-slate-400">
              Todavía no hay visitas registradas. Los datos aparecen a medida que el sitio
              recibe tráfico.
            </li>
          )}
        </ul>
      </section>
    </div>
  )
}

function Tarjeta({ titulo, valor, nota }: { titulo: string; valor: string; nota?: string }) {
  return (
    <div className="rounded-xl border border-slate-200 bg-white p-5">
      <p className="text-sm text-slate-500">{titulo}</p>
      <p className="mt-1 text-3xl font-bold">{valor}</p>
      {nota && <p className="mt-1 text-xs text-slate-400">{nota}</p>}
    </div>
  )
}
