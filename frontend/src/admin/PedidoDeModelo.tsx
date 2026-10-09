import { useState, type SyntheticEvent } from 'react'
import { api } from '@shared/api/client'
import { etiqueta } from '@shared/ui/formato'

interface Props {
  marcaId: number
  marca: string
  carrocerias: string[]
}

/**
 * "¿No está el modelo?" debajo del select. Sin esto, una automotora con un modelo que no
 * está en el catálogo se quedaba sin poder cargar la unidad y sin saber a quién pedírselo.
 *
 * El pedido le llega al administrador en Solicitudes; cuando lo aprueba, el modelo aparece
 * en la lista para todas las automotoras.
 */
export function PedidoDeModelo({ marcaId, marca, carrocerias }: Props) {
  const [abierto, setAbierto] = useState(false)
  const [nombre, setNombre] = useState('')
  const [carroceria, setCarroceria] = useState('')
  const [enviando, setEnviando] = useState(false)
  const [resultado, setResultado] = useState<{ ok: boolean; texto: string } | null>(null)

  const completo = !enviando && nombre.trim() !== '' && carroceria !== ''

  async function enviar(evento: SyntheticEvent) {
    // Vive adentro del formulario del vehículo, y un formulario dentro de otro no existe
    // en HTML: un Enter acá guardaría el vehículo. Se corta antes de que llegue.
    evento.preventDefault()
    evento.stopPropagation()

    setEnviando(true)
    setResultado(null)

    try {
      await api.catalogo.solicitar({ marcaId, nombreModelo: nombre.trim(), carroceria })
      setResultado({
        ok: true,
        texto: `Listo, pedimos que se agregue el ${marca} ${nombre.trim()}. Va a aparecer en la lista cuando lo aprueben; mientras, podés seguir cargando otras unidades.`,
      })
      setNombre('')
      setCarroceria('')
      setAbierto(false)
    } catch (problema) {
      setResultado({ ok: false, texto: problema instanceof Error ? problema.message : 'No se pudo enviar el pedido.' })
    } finally {
      setEnviando(false)
    }
  }

  return (
    <div className="mt-2 text-sm">
      {!abierto && (
        <button
          type="button"
          onClick={() => {
            setAbierto(true)
            setResultado(null)
          }}
          className="font-medium text-emerald-700 underline-offset-4 hover:underline"
        >
          ¿No está el modelo? Pedilo
        </button>
      )}

      {abierto && (
        <div
          role="group"
          aria-label="Pedir un modelo nuevo"
          className="mt-1 flex flex-col gap-3 rounded-xl border border-slate-200 bg-slate-50 p-3"
          onKeyDown={(e) => {
            if (e.key !== 'Enter') return
            e.preventDefault()
            if (completo) void enviar(e)
          }}
        >
          <p className="text-slate-600">Pedí que se agregue al catálogo de {marca}.</p>
          <div className="grid gap-2 sm:grid-cols-2">
            <input
              value={nombre}
              onChange={(e) => setNombre(e.target.value)}
              placeholder="Nombre del modelo"
              aria-label="Nombre del modelo"
              maxLength={80}
              className="panel-entrada"
            />
            <select
              value={carroceria}
              onChange={(e) => setCarroceria(e.target.value)}
              aria-label="Carrocería"
              className="panel-entrada"
            >
              <option value="">Carrocería</option>
              {carrocerias.map((opcion) => (
                <option key={opcion} value={opcion}>
                  {etiqueta(opcion)}
                </option>
              ))}
            </select>
          </div>
          <div className="flex gap-2">
            <button
              type="button"
              onClick={(e) => void enviar(e)}
              disabled={!completo}
              className="panel-boton-chico border-emerald-600 bg-emerald-600 text-white hover:border-emerald-700 hover:bg-emerald-700"
            >
              {enviando ? 'Enviando…' : 'Enviar pedido'}
            </button>
            <button type="button" onClick={() => setAbierto(false)} className="panel-boton-chico">
              Cancelar
            </button>
          </div>
        </div>
      )}

      {resultado && (
        <p role="status" className={`mt-2 ${resultado.ok ? 'text-emerald-700' : 'text-rose-600'}`}>
          {resultado.texto}
        </p>
      )}
    </div>
  )
}
