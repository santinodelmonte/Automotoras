import { useState } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { api, ApiError, sesion } from '@shared/api/client'
import { useSesion } from '@shared/auth/useSesion'

interface EstadoDeNavegacion {
  desde?: string
}

export function LoginPage() {
  const sesionActual = useSesion()
  const ubicacion = useLocation()

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  if (sesionActual) {
    const estado = ubicacion.state as EstadoDeNavegacion | null
    return <Navigate to={estado?.desde ?? '/admin'} replace />
  }

  async function entrar(evento: React.FormEvent) {
    evento.preventDefault()
    setError(null)
    setEnviando(true)

    try {
      sesion.establecer(await api.auth.login({ email, password }))
    } catch (problema) {
      setError(describir(problema))
    } finally {
      setEnviando(false)
    }
  }

  return (
    <main className="min-h-screen bg-slate-950 text-slate-100 flex items-center justify-center p-6">
      <form
        onSubmit={entrar}
        className="w-full max-w-sm rounded-2xl border border-slate-800 bg-slate-900 p-8 shadow-2xl"
      >
        <p className="text-xs font-semibold uppercase tracking-[0.2em] text-emerald-400">
          Automotora SaaS
        </p>
        <h1 className="mt-2 text-2xl font-bold text-white">Panel de administración</h1>

        <label className="mt-6 block text-sm font-medium text-slate-300" htmlFor="email">
          Email
        </label>
        <input
          id="email"
          type="email"
          autoComplete="username"
          required
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          className="mt-1 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-slate-100 outline-none focus:border-emerald-500"
        />

        <label className="mt-4 block text-sm font-medium text-slate-300" htmlFor="password">
          Contraseña
        </label>
        <input
          id="password"
          type="password"
          autoComplete="current-password"
          required
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          className="mt-1 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-slate-100 outline-none focus:border-emerald-500"
        />

        {error && (
          <p className="mt-4 rounded-lg bg-rose-950 px-3 py-2 text-sm text-rose-300">{error}</p>
        )}

        <button
          type="submit"
          disabled={enviando}
          className="mt-6 w-full rounded-lg bg-emerald-600 px-4 py-2 font-semibold text-white transition hover:bg-emerald-500 disabled:opacity-50"
        >
          {enviando ? 'Entrando…' : 'Entrar'}
        </button>
      </form>
    </main>
  )
}

function describir(problema: unknown): string {
  if (problema instanceof ApiError) {
    const porCampo = Object.values(problema.erroresPorCampo).flat()
    return porCampo.length > 0 ? porCampo.join(' ') : problema.message
  }

  return 'No se pudo contactar la API.'
}
