import { useCallback, useEffect, useState } from 'react'
import { api, ApiError } from '@shared/api/client'
import { Esqueleto, Estado } from '@shared/ui/Estado'
import { entero, fecha, precio } from '@shared/ui/formato'
import { Insignia } from '@admin/ui/Pagina'
import type { Plan, TenantAdmin, VerificacionDeDominio } from '@shared/api/types'

export function AutomotorasPage() {
  const [tenants, setTenants] = useState<TenantAdmin[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [mensaje, setMensaje] = useState<string | null>(null)
  const [errores, setErrores] = useState<Record<string, string[]>>({})
  const [creando, setCreando] = useState(false)
  const [verificaciones, setVerificaciones] = useState<Record<number, VerificacionDeDominio>>({})
  const [verificando, setVerificando] = useState<number | null>(null)
  const [restableciendo, setRestableciendo] = useState<number | null>(null)
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
      <header>
        <h1 className="panel-titulo">Automotoras</h1>
        <p className="mt-1 max-w-2xl panel-ayuda">Los clientes del SaaS: su dirección, su dominio y el acceso de sus usuarios.</p>
      </header>

      {!tenants ? (
        <Esqueleto className="h-48" />
      ) : (
        <ul className="flex flex-col gap-2">
          {tenants.map((tenant) => (
            <li
              key={tenant.id}
              className="flex flex-wrap items-center justify-between gap-3 panel-seccion p-4 sm:p-4"
            >
              <div className="min-w-0">
                <p className="flex flex-wrap items-center gap-2 font-semibold">
                  {tenant.nombre}
                  {!tenant.activo && <Insignia tono="gris">Apagada</Insignia>}
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

              {/* En el celular los conteos van en su renglón: al lado de los botones se parten. */}
              <div className="flex flex-wrap items-center gap-x-4 gap-y-2 text-sm">
                <span className="w-full whitespace-nowrap text-slate-500 sm:w-auto">
                  {entero(tenant.vehiculos)} vehículos · {entero(tenant.usuarios)} usuarios
                </span>
                <button
                  type="button"
                  onClick={() => setRestableciendo(restableciendo === tenant.id ? null : tenant.id)}
                  className="panel-boton-secundario whitespace-nowrap"
                >
                  Restablecer contraseña
                </button>
                <button
                  type="button"
                  onClick={() => void alternar(tenant)}
                  className={`whitespace-nowrap ${tenant.activo ? 'panel-boton-peligro' : 'panel-boton-secundario'}`}
                >
                  {tenant.activo ? 'Apagar sitio' : 'Reactivar'}
                </button>
              </div>

              {restableciendo === tenant.id && (
                <RestablecerPassword tenant={tenant} alTerminar={() => setRestableciendo(null)} />
              )}
            </li>
          ))}
        </ul>
      )}

      <form onSubmit={crear} className="flex flex-col gap-4 panel-seccion">
        <div>
          <h2 className="panel-seccion-titulo">Nueva automotora</h2>
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
            className="panel-boton"
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
        className="panel-boton-chico"
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

const entradaClase = 'panel-entrada'

/**
 * Le pone una contraseña provisoria a un usuario de la automotora. Al entrar con ella,
 * lo único que puede hacer es cambiarla; la provisoria se le pasa por WhatsApp o teléfono.
 */
function RestablecerPassword({ tenant, alTerminar }: { tenant: TenantAdmin; alTerminar: () => void }) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [estado, setEstado] = useState<{ tipo: 'ok' | 'error'; texto: string } | null>(null)
  const [enviando, setEnviando] = useState(false)

  async function enviar(evento: React.FormEvent) {
    evento.preventDefault()
    setEnviando(true)
    setEstado(null)

    try {
      await api.admin.restablecerPassword(tenant.id, email, password)
      setEstado({
        tipo: 'ok',
        texto: `Listo. ${email} entra con la provisoria y la cambia en el primer ingreso. Sus sesiones abiertas se cerraron.`,
      })
      setPassword('')
    } catch (problema) {
      const porCampo = problema instanceof ApiError ? Object.values(problema.erroresPorCampo).flat() : []
      setEstado({
        tipo: 'error',
        texto:
          porCampo.length > 0
            ? porCampo.join(' ')
            : problema instanceof ApiError
              ? problema.message
              : 'No se pudo restablecer la contraseña.',
      })
    } finally {
      setEnviando(false)
    }
  }

  return (
    <form onSubmit={enviar} className="flex w-full flex-col gap-3 rounded-lg bg-slate-50 p-3 sm:flex-row sm:items-end">
      <Campo etiqueta="Email del usuario">
        <input type="email" required value={email} onChange={(e) => setEmail(e.target.value)} className={entradaClase} />
      </Campo>
      <Campo etiqueta="Contraseña provisoria">
        <input
          type="text"
          required
          autoComplete="off"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          className={entradaClase}
        />
      </Campo>
      <div className="flex gap-2">
        <button
          type="submit"
          disabled={enviando}
          className="panel-boton px-3 py-2"
        >
          {enviando ? 'Guardando…' : 'Guardar'}
        </button>
        <button type="button" onClick={alTerminar} className="rounded-lg px-3 py-2 text-sm text-slate-600">
          Cerrar
        </button>
      </div>
      {estado && (
        <p className={`text-sm sm:basis-full ${estado.tipo === 'ok' ? 'text-emerald-700' : 'text-rose-600'}`}>
          {estado.texto}
        </p>
      )}
    </form>
  )
}

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
