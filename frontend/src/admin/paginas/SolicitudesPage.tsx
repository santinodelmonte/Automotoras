import { useCallback, useEffect, useState } from 'react'
import { api, ApiError } from '@shared/api/client'
import { Esqueleto, Estado } from '@shared/ui/Estado'
import { fecha } from '@shared/ui/formato'
import type { SolicitudModelo } from '@shared/api/types'

/**
 * Aprobación de las altas de modelo que piden las automotoras.
 *
 * Es lo que hace vivible la regla de normalización: prohibirle al vendedor cargar un
 * modelo que falta, sin darle por dónde pedirlo, termina con el vehículo cargado bajo el
 * modelo más parecido que encuentre — que es peor que el texto libre, porque el dato queda
 * mal y parece bien.
 */
export function SolicitudesPage() {
  const [solicitudes, setSolicitudes] = useState<SolicitudModelo[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [mensaje, setMensaje] = useState<string | null>(null)
  const [notas, setNotas] = useState<Record<number, string>>({})

  const cargar = useCallback(async (signal?: AbortSignal) => {
    try {
      setSolicitudes(await api.admin.solicitudes(undefined, signal))
    } catch (problema) {
      if (signal?.aborted) return
      setError(problema instanceof Error ? problema.message : 'No se pudieron cargar.')
    }
  }, [])

  useEffect(() => {
    const controlador = new AbortController()
    void cargar(controlador.signal)

    return () => controlador.abort()
  }, [cargar])

  async function resolver(solicitud: SolicitudModelo, aprobar: boolean) {
    setMensaje(null)

    try {
      await api.admin.resolverSolicitud(solicitud.id, {
        aprobar,
        nota: notas[solicitud.id]?.trim() || null,
      })

      setMensaje(aprobar ? 'Modelo dado de alta.' : 'Solicitud rechazada.')
      await cargar()
    } catch (problema) {
      setMensaje(problema instanceof ApiError ? problema.message : 'No se pudo resolver.')
    }
  }

  if (error) return <Estado titulo="No pudimos cargar las solicitudes" detalle={error} />
  if (!solicitudes) return <Esqueleto className="h-64" />

  const pendientes = solicitudes.filter((s) => s.estado === 'Pendiente')
  const resueltas = solicitudes.filter((s) => s.estado !== 'Pendiente')

  return (
    <div className="flex max-w-3xl flex-col gap-6">
      <header>
        <h1 className="panel-titulo">Solicitudes de modelo</h1>
        <p className="mt-1 max-w-2xl panel-ayuda">Modelos que pidieron las automotoras porque no estaban en el catálogo.</p>
      </header>

      {mensaje && <p className="text-sm text-slate-600">{mensaje}</p>}

      <section className="flex flex-col gap-3">
        <h2 className="panel-seccion-titulo">Pendientes ({pendientes.length})</h2>

        {pendientes.length === 0 && (
          <p className="panel-seccion text-sm text-slate-500">
            No hay solicitudes esperando. Cuando una automotora pida un modelo que no está en el
            catálogo, aparece acá.
          </p>
        )}

        {pendientes.map((solicitud) => (
          <div key={solicitud.id} className="panel-seccion p-4 sm:p-4">
            <p className="font-semibold">
              {solicitud.marca} {solicitud.nombreModelo}
              <span className="ml-2 text-xs font-normal text-slate-400">
                {solicitud.carroceria}
              </span>
            </p>
            <p className="mt-1 text-sm text-slate-500">
              Pedido por {solicitud.solicitadaPor} · {fecha(solicitud.createdAt)}
            </p>

            <input
              placeholder="Nota (obligatoria si se rechaza)"
              value={notas[solicitud.id] ?? ''}
              onChange={(e) => setNotas({ ...notas, [solicitud.id]: e.target.value })}
              className="mt-3 panel-entrada"
            />

            <div className="mt-3 flex gap-2">
              <button
                type="button"
                onClick={() => void resolver(solicitud, true)}
                className="panel-boton"
              >
                Aprobar y crear
              </button>
              <button
                type="button"
                onClick={() => void resolver(solicitud, false)}
                className="panel-boton-secundario"
              >
                Rechazar
              </button>
            </div>
          </div>
        ))}
      </section>

      {resueltas.length > 0 && (
        <section className="flex flex-col gap-2">
          <h2 className="panel-seccion-titulo">Resueltas</h2>

          <ul className="divide-y divide-slate-100 panel-seccion p-0 sm:p-0">
            {resueltas.map((solicitud) => (
              <li key={solicitud.id} className="px-4 py-3 text-sm">
                <p>
                  <span className="font-medium">
                    {solicitud.marca} {solicitud.nombreModelo}
                  </span>
                  <span
                    className={`ml-2 rounded px-1.5 py-0.5 text-xs ${
                      solicitud.estado === 'Aprobada'
                        ? 'bg-emerald-100 text-emerald-800'
                        : 'bg-slate-200 text-slate-600'
                    }`}
                  >
                    {solicitud.estado}
                  </span>
                </p>
                {solicitud.notaResolucion && (
                  <p className="mt-1 text-slate-500">{solicitud.notaResolucion}</p>
                )}
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  )
}
