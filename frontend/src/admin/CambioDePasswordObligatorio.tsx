import { useState } from 'react'
import { api, ApiError, sesion } from '@shared/api/client'

/**
 * Primer ingreso con una contraseña que puso otra persona: hay que cambiarla antes de
 * usar el panel.
 *
 * El servidor ya lo exige —con la provisoria responde 403 a todo lo demás—; esta pantalla
 * es para que el usuario vea por qué y lo resuelva, en vez de un panel roto.
 */
export function CambioDePasswordObligatorio() {
  const [actual, setActual] = useState('')
  const [nueva, setNueva] = useState('')
  const [repetida, setRepetida] = useState('')
  const [mensaje, setMensaje] = useState<string | null>(null)
  const [errores, setErrores] = useState<Record<string, string[]>>({})
  const [guardando, setGuardando] = useState(false)

  async function cambiar(evento: React.FormEvent) {
    evento.preventDefault()
    setMensaje(null)
    setErrores({})

    if (nueva !== repetida) {
      setMensaje('Las dos contraseñas nuevas no coinciden.')
      return
    }

    setGuardando(true)

    try {
      // La sesión nueva ya viene sin la marca: con establecerla, el panel se habilita.
      sesion.establecer(await api.auth.cambiarPassword(actual, nueva))
    } catch (problema) {
      if (problema instanceof ApiError) {
        setErrores(problema.erroresPorCampo)
        setMensaje(problema.message)
      } else {
        setMensaje('No se pudo cambiar la contraseña.')
      }
    } finally {
      setGuardando(false)
    }
  }

  return (
    <form
      onSubmit={(e) => void cambiar(e)}
      className="mx-auto flex max-w-md flex-col gap-4 panel-seccion"
    >
      <div>
        <h1 className="text-xl font-bold">Elegí tu contraseña</h1>
        <p className="mt-1 text-sm text-slate-500">
          Entraste con una contraseña provisoria. Para empezar a usar el panel, cambiala por una
          que solo sepas vos.
        </p>
      </div>

      <label className="block text-sm">
        <span className="panel-etiqueta">Contraseña provisoria</span>
        <input
          type="password"
          required
          autoComplete="current-password"
          value={actual}
          onChange={(e) => setActual(e.target.value)}
          className={entrada}
        />
      </label>

      <label className="block text-sm">
        <span className="panel-etiqueta">Contraseña nueva</span>
        <input
          type="password"
          required
          autoComplete="new-password"
          value={nueva}
          onChange={(e) => setNueva(e.target.value)}
          className={entrada}
        />
        {errores.Nueva?.map((error) => (
          <span key={error} className="panel-error">
            {error}
          </span>
        ))}
      </label>

      <label className="block text-sm">
        <span className="panel-etiqueta">Repetila</span>
        <input
          type="password"
          required
          autoComplete="new-password"
          value={repetida}
          onChange={(e) => setRepetida(e.target.value)}
          className={entrada}
        />
      </label>

      <button
        type="submit"
        disabled={guardando}
        className="panel-boton"
      >
        {guardando ? 'Guardando…' : 'Guardar y entrar'}
      </button>

      {mensaje && <p className="text-sm text-rose-700">{mensaje}</p>}
    </form>
  )
}

const entrada = 'w-full rounded-lg border border-slate-300 px-3 py-2 text-sm'
