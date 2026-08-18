import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { api } from '@shared/api/client'
import { Esqueleto, Estado } from '@shared/ui/Estado'
import { entero, kilometros, precio } from '@shared/ui/formato'
import type { EstadoVehiculo, PaginaDe, VehiculoResumen } from '@shared/api/types'

const ESTADOS: EstadoVehiculo[] = ['Disponible', 'Reservado', 'Vendido', 'Pausado']

const COLORES: Record<EstadoVehiculo, string> = {
  Disponible: 'bg-emerald-100 text-emerald-800',
  Reservado: 'bg-amber-100 text-amber-800',
  Vendido: 'bg-slate-200 text-slate-700',
  Pausado: 'bg-slate-100 text-slate-500',
}

export function VehiculosPage() {
  const [parametros, setParametros] = useSearchParams()
  const [pagina, setPagina] = useState<PaginaDe<VehiculoResumen> | null>(null)
  const [error, setError] = useState<string | null>(null)

  const filtros = useMemo(
    () => ({
      estado: (parametros.get('estado') ?? '') as EstadoVehiculo | '',
      texto: parametros.get('texto') ?? '',
      pagina: Number(parametros.get('pagina') ?? '1'),
    }),
    [parametros],
  )

  useEffect(() => {
    const controlador = new AbortController()
    setPagina(null)

    api.vehiculos
      .listar(filtros, controlador.signal)
      .then(setPagina)
      .catch((problema: unknown) => {
        if (controlador.signal.aborted) return
        setError(problema instanceof Error ? problema.message : 'No se pudo cargar el stock.')
      })

    return () => controlador.abort()
  }, [filtros])

  const cambiar = useCallback(
    (clave: string, valor: string) => {
      const siguientes = new URLSearchParams(parametros)

      if (valor === '') siguientes.delete(clave)
      else siguientes.set(clave, valor)

      siguientes.delete('pagina')
      setParametros(siguientes, { replace: true })
    },
    [parametros, setParametros],
  )

  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-bold">Vehículos</h1>

        <Link
          to="/admin/vehiculos/nuevo"
          className="rounded-lg bg-emerald-600 px-4 py-2 font-semibold text-white hover:bg-emerald-500"
        >
          Cargar vehículo
        </Link>
      </div>

      <div className="flex flex-wrap gap-3 rounded-xl border border-slate-200 bg-white p-4">
        <select
          value={filtros.estado}
          onChange={(e) => cambiar('estado', e.target.value)}
          className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
        >
          <option value="">Todos los estados</option>
          {ESTADOS.map((estado) => (
            <option key={estado} value={estado}>
              {estado}
            </option>
          ))}
        </select>

        <input
          type="search"
          placeholder="Buscar por marca, modelo o color"
          defaultValue={filtros.texto}
          onBlur={(e) => cambiar('texto', e.target.value)}
          className="min-w-56 flex-1 rounded-lg border border-slate-300 px-3 py-2 text-sm"
        />
      </div>

      {error && <Estado titulo="No pudimos cargar el stock" detalle={error} />}

      {!error && !pagina && (
        <div className="flex flex-col gap-2">
          {[0, 1, 2, 3].map((i) => (
            <Esqueleto key={i} className="h-20" />
          ))}
        </div>
      )}

      {pagina && pagina.items.length === 0 && (
        <Estado
          titulo="No hay vehículos con esos filtros"
          detalle="Probá limpiando la búsqueda o cargá el primero."
        />
      )}

      {pagina && pagina.items.length > 0 && (
        <>
          <p className="text-sm text-slate-500">{entero(pagina.total)} vehículos</p>

          <ul className="flex flex-col gap-2">
            {pagina.items.map((vehiculo) => (
              <li key={vehiculo.id}>
                <Link
                  to={`/admin/vehiculos/${vehiculo.id}`}
                  className="flex items-center gap-4 rounded-xl border border-slate-200 bg-white p-3 transition hover:border-slate-400"
                >
                  <div className="h-16 w-20 shrink-0 overflow-hidden rounded-lg bg-slate-100">
                    {vehiculo.fotoPortadaUrl ? (
                      <img
                        src={vehiculo.fotoPortadaUrl}
                        alt=""
                        loading="lazy"
                        className="h-full w-full object-cover"
                      />
                    ) : (
                      <div className="grid h-full place-items-center text-xs text-slate-400">
                        Sin foto
                      </div>
                    )}
                  </div>

                  <div className="min-w-0 flex-1">
                    <p className="truncate font-semibold">
                      {vehiculo.marca} {vehiculo.modelo}
                      {vehiculo.version && (
                        <span className="font-normal text-slate-500"> {vehiculo.version}</span>
                      )}
                    </p>
                    <p className="text-sm text-slate-500">
                      {vehiculo.anio} · {kilometros(vehiculo.kilometraje)} ·{' '}
                      {vehiculo.diasEnGondola} días en góndola
                    </p>
                  </div>

                  <div className="text-right">
                    <p className="font-bold">{precio(vehiculo.precio, vehiculo.moneda)}</p>
                    <span
                      className={`mt-1 inline-block rounded-full px-2 py-0.5 text-xs font-semibold ${COLORES[vehiculo.estado]}`}
                    >
                      {vehiculo.estado}
                    </span>
                  </div>
                </Link>
              </li>
            ))}
          </ul>

          {pagina.totalDePaginas > 1 && (
            <nav className="flex items-center justify-center gap-2">
              <button
                type="button"
                disabled={pagina.pagina <= 1}
                onClick={() => cambiarPagina(pagina.pagina - 1)}
                className="rounded-lg border border-slate-300 px-3 py-1.5 text-sm disabled:opacity-40"
              >
                Anterior
              </button>
              <span className="text-sm text-slate-500">
                {pagina.pagina} de {pagina.totalDePaginas}
              </span>
              <button
                type="button"
                disabled={pagina.pagina >= pagina.totalDePaginas}
                onClick={() => cambiarPagina(pagina.pagina + 1)}
                className="rounded-lg border border-slate-300 px-3 py-1.5 text-sm disabled:opacity-40"
              >
                Siguiente
              </button>
            </nav>
          )}
        </>
      )}
    </div>
  )

  function cambiarPagina(numero: number) {
    const siguientes = new URLSearchParams(parametros)
    siguientes.set('pagina', String(numero))
    setParametros(siguientes)
  }
}
