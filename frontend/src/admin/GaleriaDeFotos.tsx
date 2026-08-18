import { useRef, useState } from 'react'
import { api, ApiError } from '@shared/api/client'
import { achicar, nombreDeSubida } from '@admin/imagenes'
import type { VehiculoFoto } from '@shared/api/types'

interface Props {
  vehiculoId: number
  fotos: VehiculoFoto[]
  onCambio: (fotos: VehiculoFoto[]) => void
}

interface Progreso {
  subiendo: number
  total: number
}

/**
 * Galería del vehículo: subir, reordenar, elegir portada y borrar.
 *
 * Las fotos se suben de a una y achicadas en el navegador. Diez fotos de celular sin
 * achicar son decenas de megabytes por 4G, y en un solo request con las diez, un corte a
 * la novena pierde las nueve. De a una, cada foto muestra su progreso y un fallo reintenta
 * solo esa.
 */
export function GaleriaDeFotos({ vehiculoId, fotos, onCambio }: Props) {
  const entrada = useRef<HTMLInputElement>(null)
  const [progreso, setProgreso] = useState<Progreso | null>(null)
  const [error, setError] = useState<string | null>(null)

  async function subir(archivos: FileList | null) {
    if (!archivos || archivos.length === 0) return

    const lista = Array.from(archivos)
    setError(null)

    const acumuladas = [...fotos]

    for (let i = 0; i < lista.length; i++) {
      setProgreso({ subiendo: i + 1, total: lista.length })

      try {
        const archivo = lista[i]
        const comprimida = await achicar(archivo)
        const foto = await api.vehiculos.fotos.subir(
          vehiculoId,
          comprimida,
          nombreDeSubida(archivo, comprimida),
        )

        acumuladas.push(foto)
        onCambio([...acumuladas])
      } catch (problema) {
        setError(
          problema instanceof ApiError
            ? `${lista[i].name}: ${problema.message}`
            : `No se pudo subir ${lista[i].name}.`,
        )
        break
      }
    }

    setProgreso(null)

    // Se limpia la entrada para que volver a elegir el mismo archivo dispare el evento.
    if (entrada.current) entrada.current.value = ''
  }

  async function accion(operacion: () => Promise<VehiculoFoto[]>) {
    setError(null)

    try {
      onCambio(await operacion())
    } catch (problema) {
      setError(problema instanceof Error ? problema.message : 'No se pudo actualizar la galería.')
    }
  }

  async function borrar(fotoId: number) {
    setError(null)

    try {
      await api.vehiculos.fotos.borrar(vehiculoId, fotoId)
      onCambio(fotos.filter((f) => f.id !== fotoId))
    } catch (problema) {
      setError(problema instanceof Error ? problema.message : 'No se pudo borrar la foto.')
    }
  }

  function mover(indice: number, direccion: -1 | 1) {
    const destino = indice + direccion
    if (destino < 0 || destino >= fotos.length) return

    const orden = fotos.map((f) => f.id)
    ;[orden[indice], orden[destino]] = [orden[destino], orden[indice]]

    void accion(() => api.vehiculos.fotos.reordenar(vehiculoId, orden))
  }

  return (
    <section className="rounded-xl border border-slate-200 bg-white p-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 className="font-semibold">Fotos</h2>
          <p className="text-sm text-slate-500">
            La primera es la portada. Se achican solas antes de subir.
          </p>
        </div>

        <label className="cursor-pointer rounded-lg border border-slate-300 px-4 py-2 text-sm font-medium hover:border-slate-500">
          Agregar fotos
          <input
            ref={entrada}
            type="file"
            accept="image/jpeg,image/png,image/webp"
            multiple
            className="hidden"
            onChange={(e) => void subir(e.target.files)}
          />
        </label>
      </div>

      {progreso && (
        <p className="mt-3 text-sm text-slate-500">
          Subiendo {progreso.subiendo} de {progreso.total}…
        </p>
      )}

      {error && <p className="mt-3 text-sm text-rose-600">{error}</p>}

      {fotos.length === 0 ? (
        <p className="mt-4 text-sm text-slate-400">
          Todavía no hay fotos. Un vehículo sin foto casi no recibe consultas.
        </p>
      ) : (
        <ul className="mt-4 grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4">
          {fotos.map((foto, indice) => (
            <li
              key={foto.id}
              className="overflow-hidden rounded-lg border border-slate-200"
            >
              <div className="relative aspect-4/3 bg-slate-100">
                <img
                  src={foto.urlThumb ?? foto.url}
                  alt=""
                  loading="lazy"
                  className="h-full w-full object-cover"
                />
                {foto.esPortada && (
                  <span className="absolute left-1 top-1 rounded bg-emerald-600 px-1.5 py-0.5 text-xs font-semibold text-white">
                    Portada
                  </span>
                )}
              </div>

              <div className="flex items-center justify-between gap-1 p-1.5 text-xs">
                <div className="flex gap-1">
                  <button
                    type="button"
                    disabled={indice === 0}
                    onClick={() => mover(indice, -1)}
                    className="rounded border border-slate-300 px-1.5 py-0.5 disabled:opacity-30"
                    aria-label="Mover antes"
                  >
                    ←
                  </button>
                  <button
                    type="button"
                    disabled={indice === fotos.length - 1}
                    onClick={() => mover(indice, 1)}
                    className="rounded border border-slate-300 px-1.5 py-0.5 disabled:opacity-30"
                    aria-label="Mover después"
                  >
                    →
                  </button>
                </div>

                <div className="flex gap-1">
                  {!foto.esPortada && (
                    <button
                      type="button"
                      onClick={() => void accion(() => api.vehiculos.fotos.portada(vehiculoId, foto.id))}
                      className="rounded border border-slate-300 px-1.5 py-0.5"
                    >
                      Portada
                    </button>
                  )}
                  <button
                    type="button"
                    onClick={() => void borrar(foto.id)}
                    className="rounded border border-rose-300 px-1.5 py-0.5 text-rose-600"
                  >
                    Borrar
                  </button>
                </div>
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
