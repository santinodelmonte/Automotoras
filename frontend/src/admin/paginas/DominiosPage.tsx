import { useCallback, useEffect, useState } from 'react'
import { api, ApiError } from '@shared/api/client'
import { Esqueleto, Estado } from '@shared/ui/Estado'
import { fecha } from '@shared/ui/formato'
import type { Dominio, EstadoDeDominio, RegistroDns } from '@shared/api/types'

const ETIQUETAS: Record<EstadoDeDominio, { texto: string; clase: string }> = {
  Verificado: { texto: 'Verificado', clase: 'bg-emerald-100 text-emerald-900' },
  Pendiente: { texto: 'Falta el DNS', clase: 'bg-amber-100 text-amber-900' },
  Caido: { texto: 'Dejó de responder', clase: 'bg-rose-100 text-rose-900' },
}

/**
 * El alta de un dominio propio, sin que nadie del SaaS intervenga.
 *
 * La pantalla está armada alrededor de los registros DNS y no del estado: lo que la
 * automotora tiene que hacer es copiar dos líneas y pegarlas donde le administran el
 * dominio, y suele hacerlo alguien que no es quien está mirando esta pantalla.
 */
export function DominiosPage() {
  const [dominios, setDominios] = useState<Dominio[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [mensaje, setMensaje] = useState<string | null>(null)
  const [nuevo, setNuevo] = useState('')
  const [ocupado, setOcupado] = useState(false)

  const cargar = useCallback(async (signal?: AbortSignal) => {
    try {
      setDominios(await api.dominios.listar(signal))
    } catch (problema) {
      if (signal?.aborted) return
      setError(problema instanceof Error ? problema.message : 'No se pudieron cargar los dominios.')
    }
  }, [])

  useEffect(() => {
    const controlador = new AbortController()
    void cargar(controlador.signal)

    return () => controlador.abort()
  }, [cargar])

  async function ejecutar(accion: () => Promise<unknown>, exito?: string) {
    setMensaje(null)
    setOcupado(true)

    try {
      await accion()
      await cargar()

      if (exito) setMensaje(exito)
    } catch (problema) {
      setMensaje(problema instanceof ApiError ? problema.message : 'No se pudo completar la acción.')
    } finally {
      setOcupado(false)
    }
  }

  async function agregar(evento: React.FormEvent) {
    evento.preventDefault()

    await ejecutar(async () => {
      await api.dominios.agregar(nuevo)
      setNuevo('')
    }, 'Dominio agregado. Ahora creá el TXT y apretá verificar.')
  }

  if (error) return <Estado titulo="No pudimos cargar los dominios" detalle={error} />

  return (
    <div className="flex max-w-3xl flex-col gap-6">
      <div>
        <h1 className="text-2xl font-bold">Dominio propio</h1>
        <p className="mt-1 text-sm text-slate-500">
          Usá tu propio dominio en vez de la dirección que te dimos. Lo hacés vos: nosotros
          te decimos qué registros crear y verificamos cuando estén.
        </p>
      </div>

      <form onSubmit={(e) => void agregar(e)} className="flex flex-wrap gap-2">
        <input
          required
          value={nuevo}
          onChange={(e) => setNuevo(e.target.value)}
          placeholder="autosdelsur.com.uy"
          className="min-w-64 flex-1 rounded-lg border border-slate-300 px-3 py-2 font-mono text-sm"
        />
        <button
          type="submit"
          disabled={ocupado}
          className="rounded-lg bg-slate-900 px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
        >
          Agregar
        </button>
      </form>

      {mensaje && (
        <p className="rounded-lg border border-slate-200 bg-white px-4 py-3 text-sm">{mensaje}</p>
      )}

      {!dominios ? (
        <Esqueleto className="h-48" />
      ) : dominios.length === 0 ? (
        <p className="rounded-xl border border-slate-200 bg-white p-5 text-sm text-slate-400">
          Todavía no agregaste ninguno. Mientras tanto tu sitio sigue funcionando en la
          dirección que ya tenías.
        </p>
      ) : (
        <ul className="flex flex-col gap-4">
          {dominios.map((dominio) => (
            <Tarjeta
              key={dominio.id}
              dominio={dominio}
              ocupado={ocupado}
              onVerificar={() => void ejecutar(() => api.dominios.verificar(dominio.id))}
              onPrincipal={() =>
                void ejecutar(
                  () => api.dominios.principal(dominio.id),
                  'Listo: las URLs del sitio pasan a usar ese dominio.',
                )
              }
              onEliminar={() => void ejecutar(() => api.dominios.eliminar(dominio.id))}
            />
          ))}
        </ul>
      )}
    </div>
  )
}

function Tarjeta({
  dominio,
  ocupado,
  onVerificar,
  onPrincipal,
  onEliminar,
}: {
  dominio: Dominio
  ocupado: boolean
  onVerificar: () => void
  onPrincipal: () => void
  onEliminar: () => void
}) {
  const etiqueta = ETIQUETAS[dominio.estado]

  return (
    <li className="rounded-xl border border-slate-200 bg-white p-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <p className="font-mono font-semibold">{dominio.dominio}</p>
            <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${etiqueta.clase}`}>
              {etiqueta.texto}
            </span>
            {dominio.esPrincipal && (
              <span className="rounded-full bg-slate-900 px-2 py-0.5 text-xs font-semibold text-white">
                principal
              </span>
            )}
          </div>

          <p className="mt-1 text-sm text-slate-500">
            {dominio.verificadoEn
              ? `Verificado el ${fecha(dominio.verificadoEn)}`
              : 'Todavía sin verificar'}
            {dominio.ultimaVerificacion && ` · última revisión ${fecha(dominio.ultimaVerificacion)}`}
          </p>
        </div>

        <div className="flex gap-2 text-sm">
          <button
            type="button"
            disabled={ocupado}
            onClick={onVerificar}
            className="rounded-lg border border-slate-300 px-3 py-1.5 hover:border-slate-500 disabled:opacity-50"
          >
            Verificar
          </button>

          {dominio.estado === 'Verificado' && !dominio.esPrincipal && (
            <button
              type="button"
              disabled={ocupado}
              onClick={onPrincipal}
              className="rounded-lg border border-slate-300 px-3 py-1.5 hover:border-slate-500 disabled:opacity-50"
            >
              Usar como principal
            </button>
          )}

          <button
            type="button"
            disabled={ocupado}
            onClick={onEliminar}
            className="rounded-lg border border-slate-300 px-3 py-1.5 text-rose-700 hover:border-rose-400 disabled:opacity-50"
          >
            Quitar
          </button>
        </div>
      </div>

      {dominio.ultimoError && (
        <p className="mt-3 rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-900">
          {dominio.ultimoError}
        </p>
      )}

      <div className="mt-4 flex flex-col gap-3">
        <Registro registro={dominio.verificacion} />
        {dominio.paraApuntarElTrafico.map((registro) => (
          <Registro key={`${registro.tipo}-${registro.nombre}`} registro={registro} />
        ))}
      </div>
    </li>
  )
}

/**
 * Un registro DNS para copiar.
 *
 * El valor va en un `<input readOnly>` y no en un `<code>` porque lo que se hace con esto
 * es seleccionarlo entero y copiarlo, y de un bloque de texto largo se copia mal.
 */
function Registro({ registro }: { registro: RegistroDns }) {
  return (
    <div className="rounded-lg border border-slate-200 bg-slate-50 p-3">
      <div className="flex flex-wrap items-center gap-2 text-xs uppercase tracking-wide text-slate-400">
        <span className="rounded bg-slate-900 px-1.5 py-0.5 font-semibold text-white">
          {registro.tipo}
        </span>
        <span>{registro.nombre}</span>
      </div>

      <input
        readOnly
        value={registro.valor}
        onFocus={(e) => e.currentTarget.select()}
        className="mt-2 w-full rounded border border-slate-300 bg-white px-2 py-1 font-mono text-sm"
      />

      <p className="mt-2 text-sm text-slate-500">{registro.explicacion}</p>
    </div>
  )
}
