import { Component, type ErrorInfo, type ReactNode } from 'react'
import { reportarError } from '@shared/api/client'
import { Estado } from '@shared/ui/Estado'

interface Props {
  children: ReactNode
  /** Cambia con la ruta: al navegar a otra página, el error de la anterior se olvida. */
  clave: string
}

interface State {
  error: Error | null
  clave: string
}

const RECARGADO = 'automotora.recarga-por-version'

/**
 * Un error de render sin atrapar deja la pantalla en blanco. Esto lo atrapa, muestra una
 * salida y lo reporta a la API, que lo manda a Sentry con el resto de los errores.
 *
 * El caso más común no es un bug sino un deploy: quien tenía el sitio abierto pide una
 * pantalla diferida con el nombre viejo, que ya no existe en el servidor. Ahí no hay nada
 * que mostrar: se recarga una vez y listo. Una sola, para no entrar en un bucle si el
 * problema es otro.
 */
export class LimiteDeErrores extends Component<Props, State> {
  state: State = { error: null, clave: this.props.clave }

  static getDerivedStateFromError(error: Error): Partial<State> {
    return { error }
  }

  static getDerivedStateFromProps(props: Props, state: State): Partial<State> | null {
    return props.clave !== state.clave ? { error: null, clave: props.clave } : null
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    if (esVersionVieja(error) && recargarUnaVez()) return

    reportarError(error, info.componentStack ?? undefined)
  }

  render() {
    if (!this.state.error) return this.props.children

    return (
      <Estado
        titulo="Algo salió mal"
        detalle="Tuvimos un problema al mostrar esta página. Ya nos llegó el aviso."
      >
        <button
          type="button"
          onClick={() => window.location.reload()}
          className="mt-2 rounded-full bg-slate-900 px-5 py-2.5 text-sm font-semibold text-white transition hover:bg-slate-700"
        >
          Recargar la página
        </button>
      </Estado>
    )
  }
}

function esVersionVieja(error: Error) {
  return /dynamically imported module|Importing a module script failed|error loading dynamically/i.test(
    error.message,
  )
}

function recargarUnaVez() {
  try {
    // Con hora y no un simple "ya pasó": la pestaña puede seguir abierta hasta el deploy
    // siguiente, y ahí sí corresponde volver a recargar.
    const anterior = Number(sessionStorage.getItem(RECARGADO) ?? 0)
    if (Date.now() - anterior < 60_000) return false
    sessionStorage.setItem(RECARGADO, String(Date.now()))
  } catch {
    return false
  }

  window.location.reload()
  return true
}
