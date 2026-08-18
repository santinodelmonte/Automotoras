import { useCallback, useEffect, useState } from 'react'
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
      <h1 className="text-2xl font-bold">Usuarios</h1>

      <section className="rounded-xl border border-slate-200 bg-white p-5">
        <h2 className="font-semibold">De esta automotora</h2>
        <p className="mt-1 text-sm text-slate-500">
          El servidor no devuelve los de ninguna otra.
        </p>

        {!usuarios ? (
          <Esqueleto className="mt-4 h-32" />
        ) : (
          <ul className="mt-4 divide-y divide-slate-100">
            {usuarios.map((usuario) => (
              <li key={usuario.id} className="flex items-center justify-between gap-4 py-3">
                <div className="min-w-0">
                  <p className="truncate font-medium">
                    {usuario.nombre}
                    {!usuario.activo && <span className="ml-2 text-xs text-slate-400">de baja</span>}
                  </p>
                  <p className="truncate text-sm text-slate-500">
                    {usuario.email} · {usuario.rol}
                  </p>
                </div>

                {usuario.id !== sesion?.usuario.id && (
                  <button
                    type="button"
                    onClick={() => void alternar(usuario)}
                    className="shrink-0 rounded-lg border border-slate-300 px-3 py-1.5 text-sm hover:border-slate-500"
                  >
                    {usuario.activo ? 'Dar de baja' : 'Reactivar'}
                  </button>
                )}
              </li>
            ))}
          </ul>
        )}
      </section>

      <form onSubmit={crear} className="flex flex-col gap-4 rounded-xl border border-slate-200 bg-white p-5">
        <div>
          <h2 className="font-semibold">Nuevo vendedor</h2>
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
            className="rounded-lg bg-emerald-600 px-5 py-2.5 font-semibold text-white hover:bg-emerald-500 disabled:opacity-50"
          >
            {creando ? 'Creando…' : 'Crear vendedor'}
          </button>
          {mensaje && <p className="text-sm text-slate-600">{mensaje}</p>}
        </div>
      </form>
    </div>
  )
}

const entradaClase = 'w-full rounded-lg border border-slate-300 px-3 py-2 text-sm'

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
      <span className="mb-1 block font-medium text-slate-700">{etiqueta}</span>
      {children}
      {errores?.map((error) => (
        <span key={error} className="mt-1 block text-xs text-rose-600">
          {error}
        </span>
      ))}
    </label>
  )
}
