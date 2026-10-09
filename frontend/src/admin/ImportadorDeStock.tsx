import { useState } from 'react'
import { api, ApiError, guardarArchivo } from '@shared/api/client'
import type { ResultadoDeImportacion } from '@shared/api/types'

interface Props {
  /** Sin `confirmar`, valida; con `confirmar`, carga. Lo decide quien usa el componente. */
  importar: (archivo: File, confirmar: boolean) => Promise<ResultadoDeImportacion>
  /** Para refrescar lo que haga falta después de una carga exitosa. */
  alImportar?: (cantidad: number) => void
}

/**
 * Carga masiva de stock por CSV, en dos pasos: primero se valida y se ven los errores fila
 * por fila; recién con todo en orden se habilita cargar.
 *
 * Es todo o nada a propósito: un stock cargado a medias es peor que uno sin cargar, porque
 * nadie sabe qué falta.
 */
export function ImportadorDeStock({ importar, alImportar }: Props) {
  const [archivo, setArchivo] = useState<File | null>(null)
  const [resultado, setResultado] = useState<ResultadoDeImportacion | null>(null)
  const [trabajando, setTrabajando] = useState(false)
  const [mensaje, setMensaje] = useState<string | null>(null)

  async function correr(confirmar: boolean) {
    if (!archivo) return

    setTrabajando(true)
    setMensaje(null)

    try {
      const respuesta = await importar(archivo, confirmar)
      setResultado(respuesta)

      if (respuesta.importados > 0) {
        setMensaje(`Listo: se cargaron ${respuesta.importados} vehículos.`)
        setArchivo(null)
        alImportar?.(respuesta.importados)
      }
    } catch (problema) {
      setMensaje(problema instanceof ApiError ? problema.message : 'No se pudo procesar el archivo.')
    } finally {
      setTrabajando(false)
    }
  }

  async function bajarPlantilla() {
    try {
      guardarArchivo(await api.vehiculos.plantillaDeImportacion(), 'plantilla-de-stock.csv')
    } catch {
      setMensaje('No se pudo descargar la plantilla.')
    }
  }

  const sinErrores = resultado !== null && resultado.errores.length === 0 && resultado.importados === 0

  return (
    <div className="flex flex-col gap-3">
      <p className="text-sm text-slate-500">
        Completá la plantilla en Excel o Google Sheets y subila. Marca y modelo tienen que estar
        escritos como en el catálogo; los acentos y las mayúsculas no importan.
      </p>

      <div className="flex flex-wrap items-center gap-3 text-sm">
        <button
          type="button"
          onClick={() => void bajarPlantilla()}
          className="rounded-lg border border-slate-300 px-3 py-1.5 hover:border-slate-500"
        >
          Descargar plantilla
        </button>

        <input
          type="file"
          accept=".csv,text/csv"
          onChange={(e) => {
            setArchivo(e.target.files?.[0] ?? null)
            setResultado(null)
            setMensaje(null)
          }}
          className="text-sm"
        />

        <button
          type="button"
          disabled={!archivo || trabajando}
          onClick={() => void correr(false)}
          className="rounded-lg border border-slate-300 px-3 py-1.5 hover:border-slate-500 disabled:opacity-50"
        >
          {trabajando ? 'Procesando…' : 'Validar'}
        </button>

        {sinErrores && archivo && (
          <button
            type="button"
            disabled={trabajando}
            onClick={() => void correr(true)}
            className="rounded-lg bg-emerald-600 px-3 py-1.5 font-semibold text-white hover:bg-emerald-500 disabled:opacity-50"
          >
            Cargar {resultado.validas} vehículos
          </button>
        )}
      </div>

      {mensaje && <p className="text-sm text-slate-700">{mensaje}</p>}

      {resultado && resultado.importados === 0 && (
        <div className="text-sm">
          <p className="text-slate-600">
            {resultado.filas} filas leídas, {resultado.validas} sin errores.
            {resultado.errores.length === 0 && ' Todo en orden: ya se puede cargar.'}
          </p>

          {resultado.errores.length > 0 && (
            <ul className="mt-2 max-h-64 overflow-y-auto rounded-lg border border-rose-200 bg-rose-50 p-3 text-rose-800">
              {resultado.errores.map((error, i) => (
                <li key={i}>
                  {error.fila > 0 && <strong>Fila {error.fila}</strong>}
                  {error.columna && ` (${error.columna})`}
                  {error.fila > 0 && ': '}
                  {error.mensaje}
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </div>
  )
}
