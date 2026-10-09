import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { ChevronLeft, ChevronRight, ImageOff, Plus, Search } from 'lucide-react'
import { api } from '@shared/api/client'
import { Esqueleto, Estado } from '@shared/ui/Estado'
import { entero, kilometros, precio } from '@shared/ui/formato'
import type { EstadoVehiculo, PaginaDe, VehiculoResumen } from '@shared/api/types'
import { Insignia, Pagina, type Tono } from '@admin/ui/Pagina'
import { useSesion } from '@shared/auth/useSesion'

const ESTADOS: EstadoVehiculo[] = ['Disponible', 'Reservado', 'Vendido', 'Pausado']

const TONOS: Record<EstadoVehiculo, Tono> = {
  Disponible: 'verde',
  Reservado: 'ambar',
  Vendido: 'gris',
  Pausado: 'gris',
}

export function VehiculosPage() {
  const [parametros, setParametros] = useSearchParams()
  const [pagina, setPagina] = useState<PaginaDe<VehiculoResumen> | null>(null)
  const [error, setError] = useState<string | null>(null)
  const esOwner = useSesion()?.usuario.rol === 'Owner'

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
    <Pagina
      titulo="Vehículos"
      descripcion="Tu stock completo. Lo disponible es lo único que se ve en el sitio."
      acciones={
        <>
          {esOwner && (
            <Link to="/admin/plan#importar" className="panel-boton-secundario">
              Importar planilla
            </Link>
          )}
          <Link to="/admin/vehiculos/nuevo" className="panel-boton">
            <Plus aria-hidden className="size-4" />
            Cargar vehículo
          </Link>
        </>
      }
    >
      <div className="flex flex-wrap items-center gap-3">
        <div role="group" aria-label="Filtrar por estado" className="-mx-4 flex max-w-[calc(100%+2rem)] gap-1 overflow-x-auto px-4 sm:mx-0 sm:max-w-none sm:px-0">
          {['', ...ESTADOS].map((estado) => (
            <button
              key={estado || 'todos'}
              type="button"
              aria-pressed={filtros.estado === estado}
              onClick={() => cambiar('estado', estado)}
              className={`shrink-0 rounded-full border px-3.5 py-1.5 text-sm font-medium transition ${
                filtros.estado === estado
                  ? 'border-slate-900 bg-slate-900 text-white'
                  : 'border-slate-200 bg-white text-slate-600 hover:border-slate-300 hover:text-slate-900'
              }`}
            >
              {estado || 'Todos'}
            </button>
          ))}
        </div>

        <label className="relative min-w-56 flex-1">
          <span className="sr-only">Buscar en el stock</span>
          <Search aria-hidden className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-slate-400" />
          <input
            type="search"
            placeholder="Buscar por marca, modelo o color"
            defaultValue={filtros.texto}
            onBlur={(e) => cambiar('texto', e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') cambiar('texto', e.currentTarget.value)
            }}
            className="panel-entrada pl-9"
          />
        </label>
      </div>

      {error && <Estado titulo="No pudimos cargar el stock" detalle={error} />}

      {!error && !pagina && (
        <div className="flex flex-col gap-2">
          {[0, 1, 2, 3].map((i) => (
            <Esqueleto key={i} className="h-20 rounded-2xl" />
          ))}
        </div>
      )}

      {pagina && pagina.items.length === 0 && (filtros.estado || filtros.texto) && (
        <div className="panel-seccion">
          <Estado titulo="No hay vehículos con esos filtros" detalle="Probá con otro estado o limpiando la búsqueda." />
        </div>
      )}

      {/* El stock vacío de verdad es el primer día de una automotora: en vez de hablar de
          filtros que nadie puso, ofrece las dos maneras de empezar. */}
      {pagina && pagina.items.length === 0 && !filtros.estado && !filtros.texto && (
        <div className="panel-seccion">
          <Estado
            titulo="Todavía no cargaste ningún vehículo"
            detalle={
              esOwner
                ? 'Cargalos de a uno, o todos juntos con la planilla. Lo que quede Disponible aparece en tu sitio al instante.'
                : 'Cargá el primero. Lo que quede Disponible aparece en el sitio al instante.'
            }
          >
            <div className="mt-2 flex flex-wrap justify-center gap-2">
              <Link to="/admin/vehiculos/nuevo" className="panel-boton">
                <Plus aria-hidden className="size-4" />
                Cargar vehículo
              </Link>
              {esOwner && (
                <Link to="/admin/plan#importar" className="panel-boton-secundario">
                  Importar planilla
                </Link>
              )}
            </div>
          </Estado>
        </div>
      )}

      {pagina && pagina.items.length > 0 && (
        <>
          <ul className="panel-seccion divide-y divide-slate-100 p-0 sm:p-0">
            {pagina.items.map((vehiculo) => (
              <li key={vehiculo.id}>
                <Link
                  to={`/admin/vehiculos/${vehiculo.id}`}
                  className="flex items-center gap-4 px-4 py-3 transition first:rounded-t-2xl last:rounded-b-2xl hover:bg-slate-50 sm:px-5"
                >
                  <div className="h-14 w-20 shrink-0 overflow-hidden rounded-lg bg-slate-100">
                    {vehiculo.fotoPortadaUrl ? (
                      <img src={vehiculo.fotoPortadaUrl} alt="" loading="lazy" className="h-full w-full object-cover" />
                    ) : (
                      <div className="grid h-full place-items-center text-slate-300" title="Sin foto">
                        <ImageOff aria-hidden className="size-5" />
                      </div>
                    )}
                  </div>

                  <div className="min-w-0 flex-1">
                    <p className="truncate font-medium text-slate-900">
                      {vehiculo.marca} {vehiculo.modelo}
                      {vehiculo.version && <span className="font-normal text-slate-500"> {vehiculo.version}</span>}
                    </p>
                    <p className="mt-0.5 text-sm text-slate-500">
                      {vehiculo.anio} · <span className="whitespace-nowrap">{kilometros(vehiculo.kilometraje)}</span>
                      <span className="hidden sm:inline">
                        {' '}
                        · {entero(vehiculo.diasEnGondola)} {vehiculo.diasEnGondola === 1 ? 'día' : 'días'} en góndola
                      </span>
                    </p>

                    {/* En el celular el precio va abajo: al costado le come el ancho al nombre. */}
                    <div className="mt-1.5 flex items-center gap-2 sm:hidden">
                      <p className="font-semibold tabular-nums text-slate-900">{precio(vehiculo.precio, vehiculo.moneda)}</p>
                      <Insignia tono={TONOS[vehiculo.estado]}>{vehiculo.estado}</Insignia>
                    </div>
                  </div>

                  <div className="hidden shrink-0 flex-col items-end gap-1.5 sm:flex">
                    <p className="font-semibold tabular-nums text-slate-900">{precio(vehiculo.precio, vehiculo.moneda)}</p>
                    <Insignia tono={TONOS[vehiculo.estado]}>{vehiculo.estado}</Insignia>
                  </div>
                </Link>
              </li>
            ))}
          </ul>

          <div className="flex flex-wrap items-center justify-between gap-3 text-sm text-slate-500">
            <p>
              {entero(pagina.total)} {pagina.total === 1 ? 'vehículo' : 'vehículos'}
            </p>

            {pagina.totalDePaginas > 1 && (
              <nav aria-label="Páginas" className="flex items-center gap-2">
                <button
                  type="button"
                  disabled={pagina.pagina <= 1}
                  onClick={() => cambiarPagina(pagina.pagina - 1)}
                  className="panel-boton-secundario px-2.5"
                  aria-label="Página anterior"
                >
                  <ChevronLeft aria-hidden className="size-4" />
                </button>
                <span className="tabular-nums">
                  {pagina.pagina} de {pagina.totalDePaginas}
                </span>
                <button
                  type="button"
                  disabled={pagina.pagina >= pagina.totalDePaginas}
                  onClick={() => cambiarPagina(pagina.pagina + 1)}
                  className="panel-boton-secundario px-2.5"
                  aria-label="Página siguiente"
                >
                  <ChevronRight aria-hidden className="size-4" />
                </button>
              </nav>
            )}
          </div>
        </>
      )}
    </Pagina>
  )

  function cambiarPagina(numero: number) {
    const siguientes = new URLSearchParams(parametros)
    siguientes.set('pagina', String(numero))
    setParametros(siguientes)
  }
}
