import { useCallback, useEffect, useState } from 'react'
import { api, ApiError } from '@shared/api/client'
import { Esqueleto, Estado } from '@shared/ui/Estado'
import { entero, fecha } from '@shared/ui/formato'
import { precio } from '@shared/ui/formato'
import type { Plan, TenantAdmin, VerificacionDeDominio } from '@shared/api/types'

export function AutomotorasPage() {
  const [tenants, setTenants] = useState<TenantAdmin[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [mensaje, setMensaje] = useState<string | null>(null)
  const [errores, setErrores] = useState<Record<string, string[]>>({})
  const [creando, setCreando] = useState(false)
  const [verificaciones, setVerificaciones] = useState<Record<number, VerificacionDeDominio>>({})
  const [verificando, setVerificando] = useState<number | null>(null)
  const [planes, setPlanes] = useState<Plan[]>([])

  const [formulario, setFormulario] = useState({
    slug: '',
    nombre: '',
    dominioCustom: '',
    emailDelOwner: '',
    nombreDelOwner: '',
    passwordDelOwner: '',
    plan: 'demanda',
    colorPrimario: '#059669',
    whatsapp: '',
    telefono: '',
    direccion: '',
  })

  const cargar = useCallback(async (signal?: AbortSignal) => {
    try {
      const [lista, catalogo] = await Promise.all([api.admin.tenants(signal), api.admin.planes(signal)])
      setTenants(lista)
      setPlanes(catalogo.filter((p) => p.activo))
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
      await api.admin.crearTenant({
        ...formulario,
        dominioCustom: formulario.dominioCustom || null,
        whatsapp: formulario.whatsapp || null,
        telefono: formulario.telefono || null,
        direccion: formulario.direccion || null,
      })

      setFormulario({
        slug: '',
        nombre: '',
        dominioCustom: '',
        emailDelOwner: '',
        nombreDelOwner: '',
        passwordDelOwner: '',
        plan: formulario.plan,
        colorPrimario: '#059669',
        whatsapp: '',
        telefono: '',
        direccion: '',
      })
      setMensaje('Automotora creada con los dos meses bonificados. Su dueño entra con la contraseña provisoria y la cambia en el primer ingreso; el logo lo sube desde Configuración y el pago se sigue en Cobranza.')
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

  async function verificar(tenant: TenantAdmin) {
    setMensaje(null)
    setVerificando(tenant.id)

    try {
      const resultado = await api.admin.verificarDominio(tenant.id)
      setVerificaciones((previas) => ({ ...previas, [tenant.id]: resultado }))

      // Recargar la lista y no solo guardar el resultado: si quedó verificado, la fila
      // tiene que dejar de decir que no lo está.
      if (resultado.resultado === 'Verificado') await cargar()
    } catch (problema) {
      setMensaje(problema instanceof ApiError ? problema.message : 'No se pudo verificar el dominio.')
    } finally {
      setVerificando(null)
    }
  }

  async function alternar(tenant: TenantAdmin) {
    setMensaje(null)

    try {
      await api.admin.actualizarTenant(tenant.id, {
        slug: tenant.slug,
        nombre: tenant.nombre,
        dominioCustom: tenant.dominioCustom,
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
                  {tenant.dominioCustom && ` · ${tenant.dominioCustom}`} · desde{' '}
                  {fecha(tenant.createdAt)}
                </p>

                {tenant.dominioCustom && (
                  <EstadoDelDominio
                    tenant={tenant}
                    verificacion={verificaciones[tenant.id]}
                    verificando={verificando === tenant.id}
                    onVerificar={() => void verificar(tenant)}
                  />
                )}
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

          <Campo etiqueta="Plan" errores={errores.Plan}>
            <select
              value={formulario.plan}
              onChange={(e) => setFormulario({ ...formulario, plan: e.target.value })}
              className={entradaClase}
            >
              {planes.map((p) => (
                <option key={p.id} value={p.codigo}>
                  {p.nombre} — {precio(p.precioMensual, p.moneda)} por mes
                  {p.incluyeDominioPropio ? '' : ' (sin dominio propio)'}
                </option>
              ))}
            </select>
          </Campo>

          <Campo etiqueta="Dominio propio (opcional)" errores={errores.DominioCustom}>
            <input
              placeholder="automotoranorte.uy"
              value={formulario.dominioCustom}
              onChange={(e) => setFormulario({ ...formulario, dominioCustom: e.target.value })}
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

          <Campo etiqueta="Color principal" errores={errores.ColorPrimario}>
            <input
              type="color"
              value={formulario.colorPrimario}
              onChange={(e) => setFormulario({ ...formulario, colorPrimario: e.target.value })}
              className="h-10 w-20 rounded-lg border border-slate-300"
            />
          </Campo>

          <Campo etiqueta="WhatsApp (opcional)" errores={errores.Whatsapp}>
            <input
              placeholder="+59899123456"
              value={formulario.whatsapp}
              onChange={(e) => setFormulario({ ...formulario, whatsapp: e.target.value })}
              className={entradaClase}
            />
          </Campo>

          <Campo etiqueta="Teléfono (opcional)" errores={errores.Telefono}>
            <input
              value={formulario.telefono}
              onChange={(e) => setFormulario({ ...formulario, telefono: e.target.value })}
              className={entradaClase}
            />
          </Campo>

          <Campo etiqueta="Dirección (opcional)" errores={errores.Direccion}>
            <input
              value={formulario.direccion}
              onChange={(e) => setFormulario({ ...formulario, direccion: e.target.value })}
              className={entradaClase}
            />
          </Campo>

          <Campo etiqueta="Contraseña provisoria del dueño" errores={errores.PasswordDelOwner}>
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

/**
 * El estado del dominio propio de una automotora, y el botón para comprobarlo.
 *
 * Mientras no esté verificado se dice explícitamente que el sitio no responde por ahí. Un
 * dominio cargado que no funciona y no explica por qué es la clase de cosa que termina en
 * una llamada de la automotora preguntando por qué no anda su web.
 */
function EstadoDelDominio({
  tenant,
  verificacion,
  verificando,
  onVerificar,
}: {
  tenant: TenantAdmin
  verificacion?: VerificacionDeDominio
  verificando: boolean
  onVerificar: () => void
}) {
  const verificado = tenant.dominioVerificadoEn !== null

  return (
    <div className="mt-2 flex flex-wrap items-center gap-2 text-xs">
      <span
        className={`rounded-full px-2.5 py-1 font-semibold ${
          verificado ? 'bg-emerald-100 text-emerald-800' : 'bg-amber-100 text-amber-800'
        }`}
      >
        {verificado ? `Dominio verificado el ${fecha(tenant.dominioVerificadoEn)}` : 'Dominio sin verificar'}
      </span>

      {!verificado && (
        <span className="text-slate-500">
          El sitio todavía no responde por {tenant.dominioCustom}: falta apuntarlo a la aplicación.
        </span>
      )}

      <button
        type="button"
        onClick={onVerificar}
        disabled={verificando}
        className="rounded-lg border border-slate-300 px-2.5 py-1 hover:border-slate-500 disabled:opacity-50"
      >
        {verificando ? 'Verificando…' : 'Verificar'}
      </button>

      {verificacion && (
        <p className="basis-full text-slate-600">
          {verificacion.detalle}
          {verificacion.apuntaA.length > 0 && ` Hoy resuelve a ${verificacion.apuntaA.join(', ')}.`}
          {verificacion.deberiaApuntarA.length > 0 &&
            ` Tiene que apuntar a ${verificacion.deberiaApuntarA.join(' o ')}.`}
        </p>
      )}
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
