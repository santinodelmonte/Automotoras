import { useCallback, useEffect, useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { api } from '@shared/api/client'
import { idDeVisita } from '@shared/analitica/sesion'
import { Esqueleto, Estado } from '@shared/ui/Estado'
import { entero } from '@shared/ui/formato'
import { colorPrimario, useSitio } from '@public/TenantContexto'
import { VehiculoCard } from '@public/VehiculoCard'
import type {
  FiltrosDisponibles,
  FiltrosPublicos,
  PaginaDe,
  VehiculoPublicoResumen,
} from '@shared/api/types'

/**
 * Los filtros viven en el query string, no en el estado del componente.
 *
 * Es lo que hace que un listado filtrado se pueda compartir por WhatsApp, que el botón
 * "atrás" del navegador vuelva a los filtros anteriores y que recargar no borre lo que el
 * comprador venía armando.
 */
function leerFiltros(parametros: URLSearchParams): FiltrosPublicos {
  const numero = (clave: string) => {
    const valor = parametros.get(clave)
    return valor ? Number(valor) : undefined
  }

  const texto = (clave: string) => parametros.get(clave) ?? undefined

  return {
    marcaId: numero('marcaId'),
    modeloId: numero('modeloId'),
    anioDesde: numero('anioDesde'),
    anioHasta: numero('anioHasta'),
    moneda: texto('moneda'),
    precioDesde: numero('precioDesde'),
    precioHasta: numero('precioHasta'),
    kmDesde: numero('kmDesde'),
    kmHasta: numero('kmHasta'),
    combustible: texto('combustible'),
    transmision: texto('transmision'),
    carroceria: texto('carroceria'),
    orden: texto('orden'),
    pagina: numero('pagina') ?? 1,
  }
}

export function ListadoPage() {
  const { tenant, slug } = useSitio()
  const [parametros, setParametros] = useSearchParams()

  const [pagina, setPagina] = useState<PaginaDe<VehiculoPublicoResumen> | null>(null)
  const [disponibles, setDisponibles] = useState<FiltrosDisponibles | null>(null)
  const [error, setError] = useState<string | null>(null)

  const base = slug ? `/t/${slug}` : ''
  const primario = colorPrimario(tenant)
  const filtros = useMemo(() => leerFiltros(parametros), [parametros])

  useEffect(() => {
    document.title = `Vehículos — ${tenant.nombre}`
  }, [tenant.nombre])

  useEffect(() => {
    const controlador = new AbortController()
    setPagina(null)
    setError(null)

    api.publico
      .vehiculos(slug, { ...filtros, sessionId: idDeVisita() }, controlador.signal)
      .then(setPagina)
      .catch((problema: unknown) => {
        if (controlador.signal.aborted) return
        setError(problema instanceof Error ? problema.message : 'No se pudo cargar el listado.')
      })

    return () => controlador.abort()
  }, [slug, filtros])

  // Un solo request trae todo lo filtrable, y los modelos ya vienen adentro de su marca:
  // el select encadenado no necesita ir y volver cada vez que se cambia de marca.
  useEffect(() => {
    const controlador = new AbortController()

    api.publico
      .filtros(slug, controlador.signal)
      .then(setDisponibles)
      .catch(() => undefined)

    return () => controlador.abort()
  }, [slug])

  const marcas = disponibles?.marcas ?? []
  const modelos = marcas.find((m) => m.id === filtros.marcaId)?.modelos ?? []

  const cambiar = useCallback(
    (clave: string, valor: string) => {
      const siguientes = new URLSearchParams(parametros)

      if (valor === '') {
        siguientes.delete(clave)
      } else {
        siguientes.set(clave, valor)
      }

      // Cambiar un filtro siempre vuelve a la primera página: quedarse en la cuatro de un
      // resultado que ahora tiene una sola es una pantalla vacía sin explicación.
      siguientes.delete('pagina')

      // Cambiar de marca invalida el modelo elegido, que era de la marca anterior.
      if (clave === 'marcaId') siguientes.delete('modeloId')

      setParametros(siguientes, { replace: true })
    },
    [parametros, setParametros],
  )

  const irAPagina = useCallback(
    (numero: number) => {
      const siguientes = new URLSearchParams(parametros)
      siguientes.set('pagina', String(numero))
      setParametros(siguientes)
      window.scrollTo({ top: 0, behavior: 'smooth' })
    },
    [parametros, setParametros],
  )

  return (
    <div className="flex flex-col gap-6 lg:flex-row">
      <aside className="lg:w-64 lg:shrink-0">
        <div className="rounded-xl border border-slate-200 bg-white p-4">
          <div className="flex items-center justify-between">
            <h2 className="font-semibold">Filtros</h2>
            {parametros.size > 0 && (
              <button
                type="button"
                onClick={() => setParametros(new URLSearchParams(), { replace: true })}
                className="text-sm text-slate-500 underline"
              >
                Limpiar
              </button>
            )}
          </div>

          <div className="mt-4 flex flex-col gap-3">
            <Select
              etiqueta="Marca"
              valor={filtros.marcaId?.toString() ?? ''}
              onChange={(v) => cambiar('marcaId', v)}
              opciones={marcas.map((m) => ({ valor: String(m.id), texto: m.nombre }))}
            />

            <Select
              etiqueta="Modelo"
              valor={filtros.modeloId?.toString() ?? ''}
              onChange={(v) => cambiar('modeloId', v)}
              deshabilitado={!filtros.marcaId}
              opciones={modelos.map((m) => ({ valor: String(m.id), texto: m.nombre }))}
            />

            <Rango
              etiqueta="Año"
              desde={filtros.anioDesde}
              hasta={filtros.anioHasta}
              onDesde={(v) => cambiar('anioDesde', v)}
              onHasta={(v) => cambiar('anioHasta', v)}
              placeholderDesde={disponibles?.anioMinimo ?? undefined}
              placeholderHasta={disponibles?.anioMaximo ?? undefined}
            />

            <Select
              etiqueta="Moneda"
              valor={filtros.moneda ?? ''}
              onChange={(v) => cambiar('moneda', v)}
              opciones={(disponibles?.monedas ?? []).map((m) => ({ valor: m, texto: m.toUpperCase() }))}
            />

            <Rango
              etiqueta="Precio"
              desde={filtros.precioDesde}
              hasta={filtros.precioHasta}
              onDesde={(v) => cambiar('precioDesde', v)}
              onHasta={(v) => cambiar('precioHasta', v)}
              nota={
                filtros.moneda
                  ? undefined
                  : 'Elegí la moneda para poder filtrar por precio.'
              }
              deshabilitado={!filtros.moneda}
            />

            <Rango
              etiqueta="Kilometraje"
              desde={filtros.kmDesde}
              hasta={filtros.kmHasta}
              onDesde={(v) => cambiar('kmDesde', v)}
              onHasta={(v) => cambiar('kmHasta', v)}
            />

            <Select
              etiqueta="Combustible"
              valor={filtros.combustible ?? ''}
              onChange={(v) => cambiar('combustible', v)}
              opciones={(disponibles?.combustibles ?? []).map((c) => ({ valor: c, texto: c }))}
            />

            <Select
              etiqueta="Transmisión"
              valor={filtros.transmision ?? ''}
              onChange={(v) => cambiar('transmision', v)}
              opciones={(disponibles?.transmisiones ?? []).map((t) => ({ valor: t, texto: t }))}
            />

            <Select
              etiqueta="Carrocería"
              valor={filtros.carroceria ?? ''}
              onChange={(v) => cambiar('carroceria', v)}
              opciones={(disponibles?.carrocerias ?? []).map((c) => ({ valor: c, texto: c }))}
            />
          </div>
        </div>
      </aside>

      <section className="flex-1">
        <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
          <p className="text-sm text-slate-500">
            {pagina ? `${entero(pagina.total)} vehículos` : 'Buscando…'}
          </p>

          <Select
            etiqueta=""
            valor={filtros.orden ?? ''}
            onChange={(v) => cambiar('orden', v)}
            vacio="Destacados primero"
            opciones={[
              { valor: 'precio_asc', texto: 'Precio: menor a mayor' },
              { valor: 'precio_desc', texto: 'Precio: mayor a menor' },
              { valor: 'km_asc', texto: 'Menos kilómetros' },
              { valor: 'anio_desc', texto: 'Más nuevos' },
            ]}
          />
        </div>

        {error && <Estado titulo="No pudimos cargar el listado" detalle={error} />}

        {!error && !pagina && (
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
            {[0, 1, 2, 3, 4, 5].map((i) => (
              <Esqueleto key={i} className="h-72" />
            ))}
          </div>
        )}

        {pagina && pagina.items.length === 0 && (
          <Estado
            titulo="No encontramos vehículos con esos filtros"
            detalle="Probá ampliando el rango de precio o de año. Anotamos lo que buscaste: nos sirve para saber qué traer."
          />
        )}

        {pagina && pagina.items.length > 0 && (
          <>
            <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
              {pagina.items.map((vehiculo) => (
                <VehiculoCard
                  key={vehiculo.id}
                  vehiculo={vehiculo}
                  base={base}
                  colorPrimario={primario}
                />
              ))}
            </div>

            {pagina.totalDePaginas > 1 && (
              <nav className="mt-8 flex items-center justify-center gap-2">
                <button
                  type="button"
                  disabled={pagina.pagina <= 1}
                  onClick={() => irAPagina(pagina.pagina - 1)}
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
                  onClick={() => irAPagina(pagina.pagina + 1)}
                  className="rounded-lg border border-slate-300 px-3 py-1.5 text-sm disabled:opacity-40"
                >
                  Siguiente
                </button>
              </nav>
            )}
          </>
        )}
      </section>
    </div>
  )
}

function Select({
  etiqueta,
  valor,
  onChange,
  opciones,
  vacio = 'Todas',
  deshabilitado = false,
}: {
  etiqueta: string
  valor: string
  onChange: (valor: string) => void
  opciones: { valor: string; texto: string }[]
  vacio?: string
  deshabilitado?: boolean
}) {
  return (
    <label className="block text-sm">
      {etiqueta && <span className="mb-1 block font-medium text-slate-700">{etiqueta}</span>}
      <select
        value={valor}
        disabled={deshabilitado}
        onChange={(e) => onChange(e.target.value)}
        className="w-full rounded-lg border border-slate-300 bg-white px-2 py-1.5 disabled:bg-slate-100 disabled:text-slate-400"
      >
        <option value="">{vacio}</option>
        {opciones.map((o) => (
          <option key={o.valor} value={o.valor}>
            {o.texto}
          </option>
        ))}
      </select>
    </label>
  )
}

function Rango({
  etiqueta,
  desde,
  hasta,
  onDesde,
  onHasta,
  nota,
  deshabilitado = false,
  placeholderDesde,
  placeholderHasta,
}: {
  etiqueta: string
  desde: number | undefined
  hasta: number | undefined
  onDesde: (valor: string) => void
  onHasta: (valor: string) => void
  nota?: string
  deshabilitado?: boolean
  placeholderDesde?: number
  placeholderHasta?: number
}) {
  return (
    <div className="text-sm">
      <span className="mb-1 block font-medium text-slate-700">{etiqueta}</span>
      <div className="flex gap-2">
        <input
          type="number"
          inputMode="numeric"
          placeholder={placeholderDesde ? String(placeholderDesde) : 'Desde'}
          disabled={deshabilitado}
          defaultValue={desde ?? ''}
          onBlur={(e) => onDesde(e.target.value)}
          className="w-full rounded-lg border border-slate-300 px-2 py-1.5 disabled:bg-slate-100"
        />
        <input
          type="number"
          inputMode="numeric"
          placeholder={placeholderHasta ? String(placeholderHasta) : 'Hasta'}
          disabled={deshabilitado}
          defaultValue={hasta ?? ''}
          onBlur={(e) => onHasta(e.target.value)}
          className="w-full rounded-lg border border-slate-300 px-2 py-1.5 disabled:bg-slate-100"
        />
      </div>
      {nota && <p className="mt-1 text-xs text-slate-400">{nota}</p>}
    </div>
  )
}
