import { useState } from 'react'
import { api, ApiError } from '@shared/api/client'
import type { EstadoVehiculo, Vehiculo } from '@shared/api/types'

const ESTADOS: EstadoVehiculo[] = ['Disponible', 'Reservado', 'Vendido', 'Pausado']

interface Props {
  vehiculo: Vehiculo
  onCambio: (vehiculo: Vehiculo) => void
}

/**
 * Cambio rápido de estado.
 *
 * Marcar vendido no es editar un campo: pide fecha y precio de venta, y saca la unidad del
 * sitio público en el acto. Sin esos dos datos no hay días en góndola ni margen, que es la
 * mitad de para qué existe el producto — por eso el formulario los exige antes de dejar
 * confirmar.
 */
export function CambiarEstado({ vehiculo, onCambio }: Props) {
  const [pidiendoVenta, setPidiendoVenta] = useState(false)
  const [fechaVenta, setFechaVenta] = useState(new Date().toISOString().slice(0, 10))
  const [precioVenta, setPrecioVenta] = useState(String(vehiculo.precio))
  const [error, setError] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  async function aplicar(estado: EstadoVehiculo, fecha: string | null, precio: number | null) {
    setEnviando(true)
    setError(null)

    try {
      onCambio(await api.vehiculos.cambiarEstado(vehiculo.id, {
        estado,
        fechaVenta: fecha,
        precioVenta: precio,
      }))
      setPidiendoVenta(false)
    } catch (problema) {
      setError(problema instanceof ApiError ? problema.message : 'No se pudo cambiar el estado.')
    } finally {
      setEnviando(false)
    }
  }

  function elegir(estado: EstadoVehiculo) {
    if (estado === vehiculo.estado) return

    if (estado === 'Vendido') {
      setPidiendoVenta(true)
      return
    }

    void aplicar(estado, null, null)
  }

  return (
    <div className="flex flex-col items-end gap-2">
      <div className="flex items-center gap-2">
        <span className="text-sm text-slate-500">Estado</span>
        <select
          value={vehiculo.estado}
          disabled={enviando}
          onChange={(e) => elegir(e.target.value as EstadoVehiculo)}
          className="rounded-lg border border-slate-300 px-3 py-1.5 text-sm"
        >
          {ESTADOS.map((estado) => (
            <option key={estado} value={estado}>
              {estado}
            </option>
          ))}
        </select>
      </div>

      {error && <p className="text-sm text-rose-600">{error}</p>}

      {pidiendoVenta && (
        <div className="w-72 rounded-xl border border-slate-300 bg-white p-4 shadow-lg">
          <p className="font-semibold">Datos de la venta</p>
          <p className="mt-1 text-xs text-slate-500">
            Con esto se calculan los días en góndola y el margen.
          </p>

          <label className="mt-3 block text-sm">
            <span className="mb-1 block font-medium text-slate-700">Fecha</span>
            <input
              type="date"
              value={fechaVenta}
              onChange={(e) => setFechaVenta(e.target.value)}
              className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
            />
          </label>

          <label className="mt-3 block text-sm">
            <span className="mb-1 block font-medium text-slate-700">
              Precio de venta ({vehiculo.moneda.toUpperCase()})
            </span>
            <input
              type="number"
              min={1}
              step="0.01"
              value={precioVenta}
              onChange={(e) => setPrecioVenta(e.target.value)}
              className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
            />
          </label>

          <div className="mt-4 flex gap-2">
            <button
              type="button"
              disabled={enviando || !fechaVenta || Number(precioVenta) <= 0}
              onClick={() => void aplicar('Vendido', fechaVenta, Number(precioVenta))}
              className="flex-1 rounded-lg bg-emerald-600 px-3 py-2 text-sm font-semibold text-white disabled:opacity-50"
            >
              Marcar vendido
            </button>
            <button
              type="button"
              onClick={() => setPidiendoVenta(false)}
              className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
            >
              Cancelar
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
