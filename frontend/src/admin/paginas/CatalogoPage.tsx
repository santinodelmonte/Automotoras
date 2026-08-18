import { useCallback, useEffect, useState } from 'react'
import { api, ApiError } from '@shared/api/client'
import { Esqueleto, Estado } from '@shared/ui/Estado'
import type { Marca, Modelo, OpcionesDeCatalogo } from '@shared/api/types'

/**
 * Catálogo global de marcas y modelos.
 *
 * Nada se borra: se da de baja. Un modelo puede estar referenciado por vehículos ya
 * publicados, y borrarlo dejaría fichas rotas y reportes con agujeros.
 */
export function CatalogoPage() {
  const [marcas, setMarcas] = useState<Marca[] | null>(null)
  const [modelos, setModelos] = useState<Modelo[]>([])
  const [opciones, setOpciones] = useState<OpcionesDeCatalogo | null>(null)
  const [marcaId, setMarcaId] = useState<number>(0)
  const [error, setError] = useState<string | null>(null)
  const [mensaje, setMensaje] = useState<string | null>(null)

  const [nuevaMarca, setNuevaMarca] = useState('')
  const [nuevoModelo, setNuevoModelo] = useState('')
  const [carroceria, setCarroceria] = useState('Sedan')

  const cargarMarcas = useCallback(async (signal?: AbortSignal) => {
    try {
      setMarcas(await api.admin.marcas(signal))
    } catch (problema) {
      if (signal?.aborted) return
      setError(problema instanceof Error ? problema.message : 'No se pudo cargar el catálogo.')
    }
  }, [])

  const cargarModelos = useCallback(async (marca: number, signal?: AbortSignal) => {
    if (!marca) {
      setModelos([])
      return
    }

    try {
      setModelos(await api.admin.modelos(marca, signal))
    } catch {
      /* el mensaje ya se muestra desde la acción que falló */
    }
  }, [])

  useEffect(() => {
    const controlador = new AbortController()

    void cargarMarcas(controlador.signal)
    api.catalogo.opciones(controlador.signal).then(setOpciones).catch(() => undefined)

    return () => controlador.abort()
  }, [cargarMarcas])

  useEffect(() => {
    const controlador = new AbortController()
    void cargarModelos(marcaId, controlador.signal)

    return () => controlador.abort()
  }, [marcaId, cargarModelos])

  async function accion(operacion: () => Promise<unknown>, exito: string) {
    setMensaje(null)

    try {
      await operacion()
      setMensaje(exito)
      await cargarMarcas()
      await cargarModelos(marcaId)
    } catch (problema) {
      setMensaje(problema instanceof ApiError ? problema.message : 'No se pudo guardar.')
    }
  }

  if (error) return <Estado titulo="No pudimos cargar el catálogo" detalle={error} />

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-bold">Catálogo</h1>
        <p className="mt-1 text-sm text-slate-500">
          Marca, modelo y versión son tablas, nunca texto libre. Es lo que hace que la
          analítica de demanda signifique algo.
        </p>
      </div>

      {mensaje && <p className="text-sm text-slate-600">{mensaje}</p>}

      <div className="grid gap-6 lg:grid-cols-2">
        <section className="rounded-xl border border-slate-200 bg-white p-5">
          <h2 className="font-semibold">Marcas</h2>

          <div className="mt-3 flex gap-2">
            <input
              placeholder="Nueva marca"
              value={nuevaMarca}
              onChange={(e) => setNuevaMarca(e.target.value)}
              className="flex-1 rounded-lg border border-slate-300 px-3 py-2 text-sm"
            />
            <button
              type="button"
              disabled={!nuevaMarca.trim()}
              onClick={() =>
                void accion(
                  () => api.admin.crearMarca({ nombre: nuevaMarca.trim(), activo: true }),
                  'Marca creada.',
                ).then(() => setNuevaMarca(''))
              }
              className="rounded-lg bg-emerald-600 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50"
            >
              Agregar
            </button>
          </div>

          {!marcas ? (
            <Esqueleto className="mt-4 h-40" />
          ) : (
            <ul className="mt-4 max-h-96 divide-y divide-slate-100 overflow-y-auto">
              {marcas.map((marca) => (
                <li key={marca.id} className="flex items-center justify-between gap-2 py-2">
                  <button
                    type="button"
                    onClick={() => setMarcaId(marca.id)}
                    className={`flex-1 text-left ${marcaId === marca.id ? 'font-semibold' : ''} ${
                      marca.activo ? '' : 'text-slate-400 line-through'
                    }`}
                  >
                    {marca.nombre}
                  </button>

                  <button
                    type="button"
                    onClick={() =>
                      void accion(
                        () =>
                          api.admin.actualizarMarca(marca.id, {
                            nombre: marca.nombre,
                            activo: !marca.activo,
                          }),
                        marca.activo ? 'Marca dada de baja.' : 'Marca reactivada.',
                      )
                    }
                    className="rounded border border-slate-300 px-2 py-0.5 text-xs"
                  >
                    {marca.activo ? 'Baja' : 'Alta'}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </section>

        <section className="rounded-xl border border-slate-200 bg-white p-5">
          <h2 className="font-semibold">
            Modelos {marcaId > 0 && `de ${marcas?.find((m) => m.id === marcaId)?.nombre ?? ''}`}
          </h2>

          {marcaId === 0 ? (
            <p className="mt-3 text-sm text-slate-400">Elegí una marca de la lista.</p>
          ) : (
            <>
              <div className="mt-3 flex flex-wrap gap-2">
                <input
                  placeholder="Nuevo modelo"
                  value={nuevoModelo}
                  onChange={(e) => setNuevoModelo(e.target.value)}
                  className="min-w-32 flex-1 rounded-lg border border-slate-300 px-3 py-2 text-sm"
                />
                <select
                  value={carroceria}
                  onChange={(e) => setCarroceria(e.target.value)}
                  className="rounded-lg border border-slate-300 px-2 py-2 text-sm"
                >
                  {(opciones?.carrocerias ?? [carroceria]).map((valor) => (
                    <option key={valor} value={valor}>
                      {valor}
                    </option>
                  ))}
                </select>
                <button
                  type="button"
                  disabled={!nuevoModelo.trim()}
                  onClick={() =>
                    void accion(
                      () =>
                        api.admin.crearModelo({
                          marcaId,
                          nombre: nuevoModelo.trim(),
                          carroceria,
                          activo: true,
                        }),
                      'Modelo creado.',
                    ).then(() => setNuevoModelo(''))
                  }
                  className="rounded-lg bg-emerald-600 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50"
                >
                  Agregar
                </button>
              </div>

              <ul className="mt-4 max-h-96 divide-y divide-slate-100 overflow-y-auto">
                {modelos.map((modelo) => (
                  <li key={modelo.id} className="flex items-center justify-between gap-2 py-2">
                    <span className={modelo.activo ? '' : 'text-slate-400 line-through'}>
                      {modelo.nombre}
                      <span className="ml-2 text-xs text-slate-400">{modelo.carroceria}</span>
                    </span>

                    <button
                      type="button"
                      onClick={() =>
                        void accion(
                          () =>
                            api.admin.actualizarModelo(modelo.id, {
                              marcaId: modelo.marcaId,
                              nombre: modelo.nombre,
                              carroceria: modelo.carroceria,
                              activo: !modelo.activo,
                            }),
                          modelo.activo ? 'Modelo dado de baja.' : 'Modelo reactivado.',
                        )
                      }
                      className="rounded border border-slate-300 px-2 py-0.5 text-xs"
                    >
                      {modelo.activo ? 'Baja' : 'Alta'}
                    </button>
                  </li>
                ))}

                {modelos.length === 0 && (
                  <li className="py-2 text-sm text-slate-400">Esta marca no tiene modelos.</li>
                )}
              </ul>
            </>
          )}
        </section>
      </div>
    </div>
  )
}
