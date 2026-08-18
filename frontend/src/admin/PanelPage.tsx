import { useCallback, useEffect, useState } from 'react'
import { api, ApiError, sesion } from '@shared/api/client'
import { useSesion } from '@shared/auth/useSesion'
import type { Usuario } from '@shared/api/types'

export function PanelPage() {
  const sesionActual = useSesion()
  const [usuarios, setUsuarios] = useState<Usuario[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  const esOwner = sesionActual?.usuario.rol === 'Owner'

  const cargar = useCallback(async (signal?: AbortSignal) => {
    try {
      setUsuarios(await api.usuarios.listar(signal))
    } catch (problema) {
      if (signal?.aborted) return
      setError(problema instanceof ApiError ? problema.message : 'No se pudo contactar la API.')
    }
  }, [])

  useEffect(() => {
    if (!esOwner) return

    const controlador = new AbortController()
    void cargar(controlador.signal)

    return () => controlador.abort()
  }, [esOwner, cargar])

  async function salir() {
    const actual = sesion.actual()

    if (actual) {
      // Se avisa al servidor para que revoque el refresh token. Si el llamado falla, la
      // sesión local se cierra igual: dejar al usuario adentro porque no hubo red sería
      // lo peor de los dos mundos.
      try {
        await api.auth.logout(actual.refreshToken)
      } catch {
        /* el token vence solo */
      }
    }

    sesion.limpiar()
  }

  if (!sesionActual) return null

  const { usuario } = sesionActual

  return (
    <main className="min-h-screen bg-slate-950 p-6 text-slate-100">
      <div className="mx-auto max-w-3xl">
        <header className="flex items-start justify-between gap-4">
          <div>
            <p className="text-xs font-semibold uppercase tracking-[0.2em] text-emerald-400">
              Panel
            </p>
            <h1 className="mt-1 text-2xl font-bold text-white">{usuario.nombre}</h1>
            <p className="text-sm text-slate-400">
              {usuario.email} · {usuario.rol}
              {usuario.tenantId !== null && ` · automotora #${usuario.tenantId}`}
            </p>
          </div>

          <button
            type="button"
            onClick={() => void salir()}
            className="rounded-lg border border-slate-700 px-3 py-2 text-sm text-slate-300 transition hover:border-slate-500 hover:text-white"
          >
            Salir
          </button>
        </header>

        {esOwner && (
          <section className="mt-8 rounded-2xl border border-slate-800 bg-slate-900 p-6">
            <h2 className="text-lg font-semibold text-white">Usuarios de la automotora</h2>
            <p className="mt-1 text-sm text-slate-400">
              Solo los de esta automotora: el servidor no devuelve los de ninguna otra.
            </p>

            {error && <p className="mt-4 text-sm text-rose-400">{error}</p>}

            <ul className="mt-4 divide-y divide-slate-800">
              {usuarios?.map((u) => (
                <li key={u.id} className="flex items-center justify-between py-3">
                  <div>
                    <p className="font-medium text-slate-100">{u.nombre}</p>
                    <p className="text-sm text-slate-400">{u.email}</p>
                  </div>
                  <span className="text-xs uppercase tracking-wide text-slate-500">
                    {u.rol}
                    {!u.activo && ' · baja'}
                  </span>
                </li>
              ))}
            </ul>

            {usuarios?.length === 0 && (
              <p className="mt-4 text-sm text-slate-500">Todavía no hay usuarios cargados.</p>
            )}
          </section>
        )}

        <p className="mt-8 text-sm text-slate-500">
          El ABM de vehículos, las fotos y el dashboard llegan con las features de fase 1.
        </p>
      </div>
    </main>
  )
}
