import { useState, type ReactNode } from 'react'
import { Slider } from 'radix-ui'
import { ChevronDown } from 'lucide-react'
import { entero, etiqueta, simboloDeMoneda } from '@shared/ui/formato'
import type { FiltrosDisponibles, FiltrosPublicos } from '@shared/api/types'
import type { CambiosDeFiltros } from '@public/filtros'

interface Props {
  filtros: FiltrosPublicos
  disponibles: FiltrosDisponibles | null
  onCambiar: (cambios: CambiosDeFiltros) => void
}

/** Atajos de kilometraje: cubren lo que pregunta casi todo el mundo, sin tipear. */
const TOPES_DE_KILOMETRAJE = [30_000, 60_000, 100_000, 150_000]

/**
 * Los filtros del listado. Es el mismo panel en la columna de la compu y en la hoja que
 * sube desde abajo en el celular.
 *
 * Solo ofrece lo que la automotora tiene publicado: un filtro que siempre devuelve cero
 * le hace perder el tiempo al comprador.
 */
export function PanelDeFiltros({ filtros, disponibles, onCambiar }: Props) {
  const marcas = disponibles?.marcas ?? []
  const modelos = marcas.find((m) => m.id === filtros.marcaId)?.modelos ?? []
  const monedas = disponibles?.monedas ?? []

  // Con una sola moneda publicada no hay nada que elegir: el precio la toma sola. La API
  // igual la necesita, porque un rango de precio sin moneda no significa nada.
  const monedaDelPrecio = filtros.moneda ?? (monedas.length === 1 ? monedas[0] : undefined)

  const anioMinimo = disponibles?.anioMinimo
  const anioMaximo = disponibles?.anioMaximo

  return (
    <div className="flex flex-col gap-8">
      <Grupo titulo="Marca y modelo">
        <Selector
          etiqueta="Marca"
          valor={filtros.marcaId?.toString() ?? ''}
          vacio="Todas las marcas"
          opciones={marcas.map((m) => ({ valor: String(m.id), texto: m.nombre }))}
          onChange={(valor) => onCambiar({ marcaId: valor })}
        />
        <Selector
          etiqueta="Modelo"
          valor={filtros.modeloId?.toString() ?? ''}
          vacio={filtros.marcaId ? 'Todos los modelos' : 'Elegí una marca primero'}
          deshabilitado={!filtros.marcaId}
          opciones={modelos.map((m) => ({ valor: String(m.id), texto: m.nombre }))}
          onChange={(valor) => onCambiar({ modeloId: valor })}
        />
      </Grupo>

      {anioMinimo && anioMaximo && anioMinimo < anioMaximo && (
        <Grupo titulo="Año">
          {/* La key lo rearma cuando los filtros cambian desde afuera —"Limpiar", un chip
              que se saca— para que no quede mostrando el rango anterior. */}
          <RangoDeAnios
            key={`${filtros.anioDesde}-${filtros.anioHasta}`}
            minimo={anioMinimo}
            maximo={anioMaximo}
            desde={filtros.anioDesde}
            hasta={filtros.anioHasta}
            onCambiar={onCambiar}
          />
        </Grupo>
      )}

      <Grupo titulo="Precio">
        {monedas.length > 1 && (
          <Opciones
            opciones={monedas.map((m) => ({ valor: m, texto: `${etiqueta(m)} (${simboloDeMoneda(m)})` }))}
            valor={filtros.moneda}
            onElegir={(moneda) =>
              onCambiar(moneda === '' ? { moneda: '', precioDesde: '', precioHasta: '' } : { moneda })
            }
          />
        )}

        <div className="grid grid-cols-2 gap-2">
          <CampoDePrecio
            key={`desde-${filtros.precioDesde}`}
            etiqueta="Precio desde"
            placeholder="Desde"
            moneda={monedaDelPrecio}
            valor={filtros.precioDesde}
            onConfirmar={(valor) => onCambiar(conMoneda({ precioDesde: valor }, filtros, monedaDelPrecio))}
          />
          <CampoDePrecio
            key={`hasta-${filtros.precioHasta}`}
            etiqueta="Precio hasta"
            placeholder="Hasta"
            moneda={monedaDelPrecio}
            valor={filtros.precioHasta}
            onConfirmar={(valor) => onCambiar(conMoneda({ precioHasta: valor }, filtros, monedaDelPrecio))}
          />
        </div>

        {!monedaDelPrecio && monedas.length > 1 && (
          <p className="text-xs text-slate-500">Elegí la moneda para filtrar por precio.</p>
        )}
      </Grupo>

      <Grupo titulo="Kilometraje">
        <Opciones
          opciones={TOPES_DE_KILOMETRAJE.map((tope) => ({ valor: String(tope), texto: `Hasta ${entero(tope)} km` }))}
          valor={filtros.kmHasta?.toString()}
          onElegir={(valor) => onCambiar({ kmHasta: valor, kmDesde: '' })}
        />
      </Grupo>

      {(disponibles?.carrocerias.length ?? 0) > 0 && (
        <Grupo titulo="Carrocería">
          <Opciones
            opciones={(disponibles?.carrocerias ?? []).map((c) => ({ valor: c, texto: etiqueta(c) }))}
            valor={filtros.carroceria}
            onElegir={(valor) => onCambiar({ carroceria: valor })}
          />
        </Grupo>
      )}

      {(disponibles?.combustibles.length ?? 0) > 0 && (
        <Grupo titulo="Combustible">
          <Opciones
            opciones={(disponibles?.combustibles ?? []).map((c) => ({ valor: c, texto: etiqueta(c) }))}
            valor={filtros.combustible}
            onElegir={(valor) => onCambiar({ combustible: valor })}
          />
        </Grupo>
      )}

      {(disponibles?.transmisiones.length ?? 0) > 0 && (
        <Grupo titulo="Transmisión">
          <Opciones
            opciones={(disponibles?.transmisiones ?? []).map((t) => ({ valor: t, texto: etiqueta(t) }))}
            valor={filtros.transmision}
            onElegir={(valor) => onCambiar({ transmision: valor })}
          />
        </Grupo>
      )}
    </div>
  )
}

/** Si la moneda era implícita (había una sola), el precio la tiene que mandar igual. */
function conMoneda(
  cambios: CambiosDeFiltros,
  filtros: FiltrosPublicos,
  monedaDelPrecio: string | undefined,
): CambiosDeFiltros {
  return filtros.moneda || !monedaDelPrecio ? cambios : { ...cambios, moneda: monedaDelPrecio }
}

export function Selector({
  etiqueta: nombre,
  valor,
  vacio,
  opciones,
  onChange,
  deshabilitado = false,
  className = '',
}: {
  etiqueta: string
  valor: string
  vacio: string
  opciones: { valor: string; texto: string }[]
  onChange: (valor: string) => void
  deshabilitado?: boolean
  className?: string
}) {
  return (
    <label className={`relative block ${className}`}>
      <span className="sr-only">{nombre}</span>
      <select
        value={valor}
        disabled={deshabilitado}
        onChange={(evento) => onChange(evento.target.value)}
        className="w-full appearance-none rounded-xl bg-white py-2.5 pl-3.5 pr-10 text-sm font-medium text-slate-900 ring-1 ring-slate-200 transition hover:ring-slate-300 focus:outline-none focus:ring-2 focus:ring-marca disabled:bg-slate-50 disabled:text-slate-400"
      >
        <option value="">{vacio}</option>
        {opciones.map((opcion) => (
          <option key={opcion.valor} value={opcion.valor}>
            {opcion.texto}
          </option>
        ))}
      </select>
      <ChevronDown
        className="pointer-events-none absolute right-3 top-1/2 size-4 -translate-y-1/2 text-slate-400"
        aria-hidden
      />
    </label>
  )
}

function Grupo({ titulo, children }: { titulo: string; children: ReactNode }) {
  return (
    <section>
      <h3 className="mb-3 text-sm font-semibold text-slate-900">{titulo}</h3>
      <div className="flex flex-col gap-3">{children}</div>
    </section>
  )
}

/** Opciones de una sola elección que se apagan tocándolas de nuevo. */
function Opciones({
  opciones,
  valor,
  onElegir,
}: {
  opciones: { valor: string; texto: string }[]
  valor: string | undefined
  onElegir: (valor: string) => void
}) {
  return (
    <div className="flex flex-wrap gap-2">
      {opciones.map((opcion) => {
        const activa = opcion.valor === valor

        return (
          <button
            key={opcion.valor}
            type="button"
            aria-pressed={activa}
            onClick={() => onElegir(activa ? '' : opcion.valor)}
            className={`rounded-full px-3.5 py-2 text-sm font-medium ring-1 transition duration-200 active:scale-95 ${
              activa
                ? 'bg-marca text-marca-contraste ring-marca'
                : 'bg-white text-slate-700 ring-slate-200 hover:ring-slate-400'
            }`}
          >
            {opcion.texto}
          </button>
        )
      })}
    </div>
  )
}

function RangoDeAnios({
  minimo,
  maximo,
  desde,
  hasta,
  onCambiar,
}: {
  minimo: number
  maximo: number
  desde: number | undefined
  hasta: number | undefined
  onCambiar: (cambios: CambiosDeFiltros) => void
}) {
  const [valor, setValor] = useState([desde ?? minimo, hasta ?? maximo])

  const pulgar =
    'block size-6 rounded-full bg-white shadow-md ring-2 ring-marca transition-transform duration-150 hover:scale-110 focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-marca/40 active:scale-110'

  return (
    <div>
      <div className="mb-3 flex items-center justify-between text-sm font-semibold tabular-nums text-slate-900">
        <span>{valor[0]}</span>
        <span>{valor[1]}</span>
      </div>

      {/* Se aplica al soltar y no mientras se arrastra: cada año intermedio sería una
          búsqueda más en los reportes de demanda, y ninguna la hizo una persona. */}
      <Slider.Root
        min={minimo}
        max={maximo}
        step={1}
        value={valor}
        onValueChange={setValor}
        onValueCommit={([anioDesde, anioHasta]) =>
          onCambiar({
            anioDesde: anioDesde === minimo ? '' : String(anioDesde),
            anioHasta: anioHasta === maximo ? '' : String(anioHasta),
          })
        }
        className="relative flex h-6 touch-none select-none items-center"
      >
        <Slider.Track className="relative h-1.5 grow overflow-hidden rounded-full bg-slate-200">
          <Slider.Range className="absolute h-full rounded-full bg-marca" />
        </Slider.Track>
        <Slider.Thumb aria-label="Año desde" className={pulgar} />
        <Slider.Thumb aria-label="Año hasta" className={pulgar} />
      </Slider.Root>
    </div>
  )
}

function CampoDePrecio({
  etiqueta: nombre,
  placeholder,
  moneda,
  valor,
  onConfirmar,
}: {
  etiqueta: string
  placeholder: string
  moneda: string | undefined
  valor: number | undefined
  onConfirmar: (valor: string) => void
}) {
  const deshabilitado = !moneda

  // Se acepta "25.000", "25000" o "US$ 25.000": cuentan solo los dígitos.
  function confirmar(texto: string) {
    const digitos = texto.replace(/\D/g, '')
    if (digitos !== (valor?.toString() ?? '')) onConfirmar(digitos)
  }

  return (
    <label
      className={`flex items-center gap-1.5 rounded-xl bg-white px-3 ring-1 ring-slate-200 transition focus-within:ring-2 focus-within:ring-marca ${
        deshabilitado ? 'opacity-50' : ''
      }`}
    >
      <span className="sr-only">{nombre}</span>
      {moneda && <span className="text-sm text-slate-400">{simboloDeMoneda(moneda)}</span>}
      <input
        type="text"
        inputMode="numeric"
        placeholder={placeholder}
        defaultValue={valor ? entero(valor) : ''}
        disabled={deshabilitado}
        onBlur={(evento) => confirmar(evento.target.value)}
        onKeyDown={(evento) => {
          if (evento.key === 'Enter') evento.currentTarget.blur()
        }}
        className="w-full min-w-0 bg-transparent py-2.5 text-sm text-slate-900 outline-none placeholder:text-slate-400"
      />
    </label>
  )
}
