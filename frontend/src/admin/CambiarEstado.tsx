import { useState } from 'react'
import { api, ApiError } from '@shared/api/client'
import type { EstadoVehiculo, Vehiculo } from '@shared/api/types'
import { EntradaNumerica } from '@admin/ui/EntradaNumerica'

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
  const [precioVenta, setPrecioVenta] = useState<number | null>(vehiculo.precio)
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
          className="panel-entrada w-auto font-medium"
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
            <span className="panel-etiqueta">Fecha</span>
            <input
              type="date"
              value={fechaVenta}
              onChange={(e) => setFechaVenta(e.target.value)}
              className="panel-entrada"
            />
          </label>

          <label className="mt-3 block text-sm">
            <span className="panel-etiqueta">
              Precio de venta
            </span>
            <EntradaNumerica moneda={vehiculo.moneda} valor={precioVenta} onCambio={setPrecioVenta} />
          </label>

          <div className="mt-4 flex gap-2">
            <button
              type="button"
              disabled={enviando || !fechaVenta || !precioVenta || precioVenta <= 0}
              onClick={() => void aplicar('Vendido', fechaVenta, precioVenta)}
              className="flex-1 panel-boton px-3 py-2"
            >
              Marcar vendido
            </button>
            <button
              type="button"
              onClick={() => setPidiendoVenta(false)}
              className="panel-entrada w-auto"
            >
              Cancelar
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
