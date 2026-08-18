import { useCallback, useEffect, useState } from 'react'
import { api, ApiError } from '@shared/api/client'
import { Esqueleto, Estado } from '@shared/ui/Estado'
import { entero, fecha } from '@shared/ui/formato'
import type { TenantAdmin } from '@shared/api/types'

export function AutomotorasPage() {
  const [tenants, setTenants] = useState<TenantAdmin[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [mensaje, setMensaje] = useState<string | null>(null)
  const [errores, setErrores] = useState<Record<string, string[]>>({})
  const [creando, setCreando] = useState(false)

  const [formulario, setFormulario] = useState({
    slug: '',
    nombre: '',
    emailDelOwner: '',
    nombreDelOwner: '',
    passwordDelOwner: '',
  })

  const cargar = useCallback(async (signal?: AbortSignal) => {
    try {
      setTenants(await api.admin.tenants(signal))
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
      await api.admin.crearTenant(formulario)

      setFormulario({
        slug: '',
        nombre: '',
        emailDelOwner: '',
        nombreDelOwner: '',
        passwordDelOwner: '',
      })
      setMensaje('Automotora creada. Su dueño ya puede entrar.')
      await cargar()
    } catch (problema) {
      if (problema instanceof ApiError) {
        setErrores(problema.erroresPorCampo)
        setMensaje(problema.message)
      } else {
        setMensaje('No se pudo crear la automotora.')
      }
    } finally {
      setCreando(false)
    }
  }

  async function alternar(tenant: TenantAdmin) {
    setMensaje(null)

    try {
      await api.admin.actualizarTenant(tenant.id, {
        slug: tenant.slug,
        nombre: tenant.nombre,
        activo: !tenant.activo,
      })
      await cargar()
    } catch (problema) {
      setMensaje(problema instanceof ApiError ? problema.message : 'No se pudo actualizar.')
    }
  }

  if (error) return <Estado titulo="No pudimos cargar las automotoras" detalle={error} />

  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-2xl font-bold">Automotoras</h1>

      {!tenants ? (
        <Esqueleto className="h-48" />
      ) : (
        <ul className="flex flex-col gap-2">
          {tenants.map((tenant) => (
            <li
              key={tenant.id}
              className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-slate-200 bg-white p-4"
            >
              <div className="min-w-0">
                <p className="font-semibold">
                  {tenant.nombre}
                  {!tenant.activo && (
                    <span className="ml-2 rounded bg-slate-200 px-1.5 py-0.5 text-xs text-slate-600">
                      apagada
                    </span>
                  )}
                </p>
                <p className="text-sm text-slate-500">
                  /t/{tenant.slug}
                  {tenant.dominioPrincipal && ` · ${tenant.dominioPrincipal}`} · desde{' '}
                  {fecha(tenant.createdAt)}
                </p>
              </div>

              <div className="flex items-center gap-4 text-sm">
                <span className="text-slate-500">
                  {entero(tenant.vehiculos)} vehículos · {entero(tenant.usuarios)} usuarios
                </span>
                <button
                  type="button"
                  onClick={() => void alternar(tenant)}
                  className="rounded-lg border border-slate-300 px-3 py-1.5 hover:border-slate-500"
                >
                  {tenant.activo ? 'Apagar sitio' : 'Reactivar'}
                </button>
              </div>
            </li>
          ))}
        </ul>
      )}

      <form onSubmit={crear} className="flex flex-col gap-4 rounded-xl border border-slate-200 bg-white p-5">
        <div>
          <h2 className="font-semibold">Nueva automotora</h2>
          <p className="mt-1 text-sm text-slate-500">
            Se crea junto con su dueño: una automotora sin nadie que pueda entrar no sirve
            para nada.
          </p>
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          <Campo etiqueta="Nombre" errores={errores.Nombre}>
            <input
              required
              value={formulario.nombre}
              onChange={(e) => setFormulario({ ...formulario, nombre: e.target.value })}
              className={entradaClase}
            />
          </Campo>

          <Campo etiqueta="Slug" errores={errores.Slug}>
            <input
              required
              placeholder="automotora-norte"
              value={formulario.slug}
              onChange={(e) => setFormulario({ ...formulario, slug: e.target.value })}
              className={entradaClase}
            />
          </Campo>

          <Campo etiqueta="Nombre del dueño" errores={errores.NombreDelOwner}>
            <input
              required
              value={formulario.nombreDelOwner}
              onChange={(e) => setFormulario({ ...formulario, nombreDelOwner: e.target.value })}
              className={entradaClase}
            />
          </Campo>

          <Campo etiqueta="Email del dueño" errores={errores.EmailDelOwner}>
            <input
              type="email"
              required
              value={formulario.emailDelOwner}
              onChange={(e) => setFormulario({ ...formulario, emailDelOwner: e.target.value })}
              className={entradaClase}
            />
          </Campo>

          <Campo etiqueta="Contraseña del dueño" errores={errores.PasswordDelOwner}>
            <input
              type="password"
              required
              autoComplete="new-password"
              value={formulario.passwordDelOwner}
              onChange={(e) => setFormulario({ ...formulario, passwordDelOwner: e.target.value })}
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
            {creando ? 'Creando…' : 'Crear automotora'}
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
