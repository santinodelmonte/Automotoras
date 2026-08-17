import { useEffect, useState } from 'react'
import { api, ApiError } from '@shared/api/client'
import type { HealthStatus } from '@shared/api/types'

type State =
  | { kind: 'loading' }
  | { kind: 'ok'; health: HealthStatus }
  | { kind: 'error'; message: string }

function App() {
  const [state, setState] = useState<State>({ kind: 'loading' })

  useEffect(() => {
    const controller = new AbortController()

    api
      .health(controller.signal)
      .then((health) => setState({ kind: 'ok', health }))
      .catch((error: unknown) => {
        if (controller.signal.aborted) return
        setState({ kind: 'error', message: describeError(error) })
      })

    return () => controller.abort()
  }, [])

  return (
    <main className="min-h-screen bg-slate-950 text-slate-100 flex items-center justify-center p-6">
      <div className="w-full max-w-md rounded-2xl border border-slate-800 bg-slate-900 p-8 shadow-2xl">
        <p className="text-xs font-semibold uppercase tracking-[0.2em] text-emerald-400">
          Automotora SaaS
        </p>
        <h1 className="mt-2 text-2xl font-bold text-white">Esqueleto ejecutable</h1>
        <p className="mt-1 text-sm text-slate-400">
          Verificación de conectividad con <code className="text-slate-300">GET /api/health</code>
        </p>

        <div className="mt-6 rounded-xl bg-slate-950 p-4 font-mono text-sm">
          {state.kind === 'loading' && <span className="text-slate-400">Consultando la API…</span>}

          {state.kind === 'ok' && (
            <div className="space-y-1">
              <p className="text-emerald-400">status: {state.health.status}</p>
              <p className="text-slate-400 break-all">timestamp: {state.health.timestamp}</p>
            </div>
          )}

          {state.kind === 'error' && <p className="text-rose-400">{state.message}</p>}
        </div>

        <p className="mt-4 text-xs text-slate-500 break-all">API: {api.baseUrl}</p>
      </div>
    </main>
  )
}

function describeError(error: unknown): string {
  if (error instanceof ApiError) return `${error.status} — ${error.message}`
  if (error instanceof Error) return `No se pudo contactar la API: ${error.message}`
  return 'No se pudo contactar la API.'
}

export default App
