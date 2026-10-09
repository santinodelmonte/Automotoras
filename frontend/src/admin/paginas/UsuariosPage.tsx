import { useCallback, useEffect, useState } from 'react'
import { Insignia } from '@admin/ui/Pagina'
import { api, ApiError } from '@shared/api/client'
import { useSesion } from '@shared/auth/useSesion'
import { Esqueleto, Estado } from '@shared/ui/Estado'
import type { Usuario } from '@shared/api/types'

export function UsuariosPage() {
  const sesion = useSesion()
  const [usuarios, setUsuarios] = useState<Usuario[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  const [email, setEmail] = useState('')
  const [nombre, setNombre] = useState('')
  const [password, setPassword] = useState('')
  const [errores, setErrores] = useState<Record<string, string[]>>({})
  const [mensaje, setMensaje] = useState<string | null>(null)
  const [creando, setCreando] = useState(false)

  const cargar = useCallback(async (signal?: AbortSignal) => {
    try {
      setUsuarios(await api.usuarios.listar(signal))
    } catch (problema) {
      if (signal?.aborted) return
      setError(problema instanceof Error ? problema.message : 'No se pudo cargar la lista.')
    }
  }, [])

  useEffect(() => {
    const controlador = new AbortController()
    void cargar(controlador.signal)

    return () => controlador.abort()
  }, [cargar])

  async function crear(evento: React.FormEvent) {
    evento.preventDefault()
    setErrores({})
    setMensaje(null)
    setCreando(true)

    try {
      await api.usuarios.crear({ email, nombre, password, rol: 'Seller' })
      setEmail('')
      setNombre('')
      setPassword('')
      setMensaje('Vendedor creado.')
      await cargar()
    } catch (problema) {
      if (problema instanceof ApiError) {
        setErrores(problema.erroresPorCampo)
        setMensaje(problema.message)
      } else {
        setMensaje('No se pudo crear el vendedor.')
      }
    } finally {
      setCreando(false)
    }
  }

  async function alternar(usuario: Usuario) {
    setMensaje(null)

    try {
      await api.usuarios.actualizar(usuario.id, {
        nombre: usuario.nombre,
        activo: !usuario.activo,
      })
      await cargar()
    } catch (problema) {
      setMensaje(problema instanceof ApiError ? problema.message : 'No se pudo actualizar.')
    }
  }

  if (error) return <Estado titulo="No pudimos cargar los usuarios" detalle={error} />

  return (
    <div className="flex max-w-3xl flex-col gap-6">
      <header>
        <h1 className="panel-titulo">Usuarios</h1>
        <p className="mt-1 max-w-2xl panel-ayuda">Quién entra al panel de tu automotora.</p>
      </header>

      <section className="panel-seccion">
        <h2 className="panel-seccion-titulo">De esta automotora</h2>
        <p className="mt-1 text-sm text-slate-500">
          El servidor no devuelve los de ninguna otra.
        </p>

        {!usuarios ? (
          <Esqueleto className="mt-4 h-32" />
        ) : (
          <ul className="mt-4 divide-y divide-slate-100">
            {usuarios.map((usuario) => (
              <li key={usuario.id} className="flex items-center justify-between gap-4 py-3">
                <div className="flex min-w-0 items-center gap-3">
                  <span
                    aria-hidden
                    className={`grid size-9 shrink-0 place-items-center rounded-full text-sm font-semibold ${
                      usuario.activo ? 'bg-slate-100 text-slate-700' : 'bg-slate-50 text-slate-300'
                    }`}
                  >
                    {usuario.nombre.trim().charAt(0).toUpperCase()}
                  </span>
                  <div className="min-w-0">
                    <p className={`flex flex-wrap items-center gap-2 font-medium ${usuario.activo ? '' : 'text-slate-400'}`}>
                      <span className="truncate">{usuario.nombre}</span>
                      <Insignia tono={usuario.rol === 'Owner' ? 'violeta' : 'azul'}>
                        {usuario.rol === 'Owner' ? 'Dueño' : 'Vendedor'}
                      </Insignia>
                      {!usuario.activo && <Insignia tono="gris">De baja</Insignia>}
                    </p>
                    <p className="truncate text-sm text-slate-500">{usuario.email}</p>
                  </div>
                </div>

                {usuario.id !== sesion?.usuario.id && (
                  <button
                    type="button"
                    onClick={() => void alternar(usuario)}
                    className="shrink-0 panel-boton-secundario"
                  >
                    {usuario.activo ? 'Dar de baja' : 'Reactivar'}
                  </button>
                )}
              </li>
            ))}
          </ul>
        )}
      </section>

      <form onSubmit={crear} className="flex flex-col gap-4 panel-seccion">
        <div>
          <h2 className="panel-seccion-titulo">Nuevo vendedor</h2>
          <p className="mt-1 text-sm text-slate-500">
            Los vendedores cargan y editan vehículos, y ven las consultas. No acceden a
            reportes, ni a la analítica, ni al precio de costo.
          </p>
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          <Campo etiqueta="Nombre" errores={errores.Nombre}>
            <input
              required
              value={nombre}
              onChange={(e) => setNombre(e.target.value)}
              className={entradaClase}
            />
          </Campo>

          <Campo etiqueta="Email" errores={errores.Email}>
            <input
              type="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              className={entradaClase}
            />
          </Campo>

          <Campo etiqueta="Contraseña" errores={errores.Password}>
            <input
              type="password"
              required
              autoComplete="new-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className={entradaClase}
            />
          </Campo>
        </div>

        <div className="flex items-center gap-3">
          <button
            type="submit"
            disabled={creando}
            className="panel-boton"
          >
            {creando ? 'Creando…' : 'Crear vendedor'}
          </button>
          {mensaje && <p className="text-sm text-slate-600">{mensaje}</p>}
        </div>
      </form>
    </div>
  )
}

const entradaClase = 'panel-entrada'

function Campo({
  etiqueta,
  children,
  errores,
}: {
  etiqueta: string
  children: React.ReactNode
  errores?: string[]
}) {
  return (
    <label className="block text-sm">
      <span className="panel-etiqueta">{etiqueta}</span>
      {children}
      {errores?.map((error) => (
        <span key={error} className="panel-error">
          {error}
        </span>
      ))}
    </label>
  )
}
