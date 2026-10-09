import { useState } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { SearchX, TrendingUp, Zap } from 'lucide-react'
import { api, ApiError, sesion } from '@shared/api/client'
import { useSesion } from '@shared/auth/useSesion'
import { Marca } from '@admin/ui/Marca'

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
    <main className="grid min-h-screen bg-white text-slate-900 lg:grid-cols-[1.1fr_1fr]">
      {/* La propuesta, del lado izquierdo. En el celular sobra: ahí se entra directo al formulario. */}
      <section className="relative hidden overflow-hidden bg-slate-950 p-12 text-white lg:flex lg:flex-col lg:justify-between">
        <div
          aria-hidden
          className="pointer-events-none absolute -left-32 -top-32 size-[520px] rounded-full bg-emerald-500/25 blur-3xl"
        />
        <div
          aria-hidden
          className="pointer-events-none absolute -bottom-40 right-0 size-[420px] rounded-full bg-sky-500/15 blur-3xl"
        />

        <div className="relative">
          <Marca oscuro />
        </div>

        <div className="relative max-w-md">
          <h2 className="text-4xl font-semibold leading-tight tracking-tight">
            Comprá el stock que tus clientes ya están buscando.
          </h2>
          <p className="mt-4 text-slate-300">
            Tu sitio, tu stock y lo que piden los compradores, en un solo lugar.
          </p>

          <ul className="mt-10 flex flex-col gap-5">
            <Punto icono={SearchX} titulo="Lo que buscan y no tenés">
              Cada búsqueda sin resultado queda registrada: es demanda concreta.
            </Punto>
            <Punto icono={TrendingUp} titulo="Qué se mira y qué se consulta">
              Unidad por unidad, para ver qué está caro y qué no se ve.
            </Punto>
            <Punto icono={Zap} titulo="Publicar en minutos">
              Cargá un auto desde el celular, con las fotos achicadas solas.
            </Punto>
          </ul>
        </div>

        <p className="relative text-xs text-slate-500">© {new Date().getFullYear()} Automotora SaaS</p>
      </section>

      <section className="flex items-center justify-center p-6 sm:p-12">
        <form onSubmit={entrar} className="w-full max-w-sm">
          <div className="lg:hidden">
            <Marca />
          </div>

          <h1 className="mt-8 text-2xl font-semibold tracking-tight lg:mt-0">Entrá a tu panel</h1>
          <p className="mt-1 panel-ayuda">Con el email y la contraseña de tu cuenta.</p>

          <label className="mt-8 block text-sm" htmlFor="email">
            <span className="panel-etiqueta">Email</span>
          </label>
          <input
            id="email"
            type="email"
            autoComplete="username"
            required
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            className="panel-entrada py-2.5"
          />

          <label className="mt-4 block text-sm" htmlFor="password">
            <span className="panel-etiqueta">Contraseña</span>
          </label>
          <input
            id="password"
            type="password"
            autoComplete="current-password"
            required
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            className="panel-entrada py-2.5"
          />

          {error && (
            <p role="alert" className="mt-4 rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-700">
              {error}
            </p>
          )}

          <button type="submit" disabled={enviando} className="mt-6 w-full panel-boton py-3">
            {enviando ? 'Entrando…' : 'Entrar'}
          </button>

          <p className="mt-6 text-xs leading-relaxed text-slate-500">
            ¿Te olvidaste la contraseña? Si sos vendedor, pedile al dueño de la automotora que te
            ponga una provisoria. Si sos el dueño, escribinos a soporte.
          </p>
        </form>
      </section>
    </main>
  )
}

function Punto({
  icono: Icono,
  titulo,
  children,
}: {
  icono: typeof SearchX
  titulo: string
  children: React.ReactNode
}) {
  return (
    <li className="flex gap-4">
      <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-white/10 text-emerald-300 ring-1 ring-white/10">
        <Icono aria-hidden className="size-5" />
      </span>
      <span>
        <span className="block font-medium">{titulo}</span>
        <span className="text-sm text-slate-400">{children}</span>
      </span>
    </li>
  )
}

function describir(problema: unknown): string {
  if (problema instanceof ApiError) {
    // El tope por IP corta antes del controller y no trae detalle.
    if (problema.status === 429 && !problema.problem?.detail) {
      return 'Demasiados intentos seguidos. Esperá un minuto y probá de nuevo.'
    }

    const porCampo = Object.values(problema.erroresPorCampo).flat()
    return porCampo.length > 0 ? porCampo.join(' ') : problema.message
  }

  return 'No se pudo contactar la API.'
}
