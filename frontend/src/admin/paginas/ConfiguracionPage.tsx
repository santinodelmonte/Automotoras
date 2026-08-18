import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { api, ApiError } from '@shared/api/client'
import { achicar, nombreDeSubida } from '@admin/imagenes'
import { Esqueleto, Estado } from '@shared/ui/Estado'
import type { ConfiguracionDeTenant } from '@shared/api/types'

export function ConfiguracionPage() {
  const entrada = useRef<HTMLInputElement>(null)
  const [configuracion, setConfiguracion] = useState<ConfiguracionDeTenant | null>(null)
  const [errores, setErrores] = useState<Record<string, string[]>>({})
  const [mensaje, setMensaje] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [guardando, setGuardando] = useState(false)

  useEffect(() => {
    const controlador = new AbortController()

    api.tenant
      .obtener(controlador.signal)
      .then(setConfiguracion)
      .catch((problema: unknown) => {
        if (controlador.signal.aborted) return
        setError(problema instanceof Error ? problema.message : 'No se pudo cargar la configuración.')
      })

    return () => controlador.abort()
  }, [])

  if (error) return <Estado titulo="No pudimos cargar la configuración" detalle={error} />
  if (!configuracion) return <Esqueleto className="h-96" />

  const actual = configuracion

  function cambiar<C extends keyof ConfiguracionDeTenant>(clave: C, valor: ConfiguracionDeTenant[C]) {
    setConfiguracion((previa) => (previa ? { ...previa, [clave]: valor } : previa))
  }

  async function guardar(evento: React.FormEvent) {
    evento.preventDefault()
    setErrores({})
    setMensaje(null)
    setGuardando(true)

    try {
      setConfiguracion(
        await api.tenant.guardar({
          nombre: actual.nombre,
          colorPrimario: actual.colorPrimario,
          colorSecundario: actual.colorSecundario,
          whatsapp: actual.whatsapp,
          telefono: actual.telefono,
          direccion: actual.direccion,
        }),
      )
      setMensaje('Guardado. El sitio público ya se ve con estos datos.')
    } catch (problema) {
      if (problema instanceof ApiError) {
        setErrores(problema.erroresPorCampo)
        setMensaje(problema.message)
      } else {
        setMensaje('No se pudo guardar.')
      }
    } finally {
      setGuardando(false)
    }
  }

  async function subirLogo(archivos: FileList | null) {
    if (!archivos || archivos.length === 0) return

    setMensaje(null)

    try {
      const archivo = archivos[0]
      const comprimido = await achicar(archivo, 600, 0.9)

      setConfiguracion(await api.tenant.logo(comprimido, nombreDeSubida(archivo, comprimido)))
      setMensaje('Logo actualizado.')
    } catch (problema) {
      setMensaje(problema instanceof ApiError ? problema.message : 'No se pudo subir el logo.')
    } finally {
      if (entrada.current) entrada.current.value = ''
    }
  }

  return (
    <div className="flex max-w-2xl flex-col gap-6">
      <h1 className="text-2xl font-bold">Configuración</h1>

      <section className="rounded-xl border border-slate-200 bg-white p-5">
        <h2 className="font-semibold">Dirección del sitio</h2>
        <p className="mt-1 text-sm text-slate-500">
          El slug lo administra el equipo del SaaS: cambiarlo apaga la dirección por la que tu
          sitio ya está circulando. Tu dominio propio lo manejás vos, en{' '}
          <Link to="/admin/dominios" className="underline">
            Dominio
          </Link>
          .
        </p>

        <dl className="mt-3 text-sm">
          <dt className="text-slate-500">Slug</dt>
          <dd className="font-mono">/t/{actual.slug}</dd>
        </dl>
      </section>

      <section className="rounded-xl border border-slate-200 bg-white p-5">
        <div className="flex items-center justify-between gap-4">
          <div>
            <h2 className="font-semibold">Logo</h2>
            <p className="text-sm text-slate-500">Se achica solo antes de subir.</p>
          </div>

          <label className="cursor-pointer rounded-lg border border-slate-300 px-4 py-2 text-sm font-medium hover:border-slate-500">
            Cambiar
            <input
              ref={entrada}
              type="file"
              accept="image/jpeg,image/png,image/webp"
              className="hidden"
              onChange={(e) => void subirLogo(e.target.files)}
            />
          </label>
        </div>

        {actual.logoUrl && (
          <img src={actual.logoUrl} alt="Logo" className="mt-4 h-16 w-auto" />
        )}
      </section>

      <form onSubmit={guardar} className="flex flex-col gap-4 rounded-xl border border-slate-200 bg-white p-5">
        <Campo etiqueta="Nombre" errores={errores.Nombre}>
          <input
            required
            value={actual.nombre}
            onChange={(e) => cambiar('nombre', e.target.value)}
            className={entradaClase}
          />
        </Campo>

        <div className="grid gap-4 sm:grid-cols-2">
          <Campo etiqueta="Color primario" errores={errores.ColorPrimario}>
            <div className="flex gap-2">
              <input
                type="color"
                value={actual.colorPrimario ?? '#0f172a'}
                onChange={(e) => cambiar('colorPrimario', e.target.value)}
                className="h-10 w-14 rounded-lg border border-slate-300"
              />
              <input
                placeholder="#059669"
                value={actual.colorPrimario ?? ''}
                onChange={(e) => cambiar('colorPrimario', e.target.value || null)}
                className={entradaClase}
              />
            </div>
          </Campo>

          <Campo etiqueta="Color secundario" errores={errores.ColorSecundario}>
            <div className="flex gap-2">
              <input
                type="color"
                value={actual.colorSecundario ?? '#0f172a'}
                onChange={(e) => cambiar('colorSecundario', e.target.value)}
                className="h-10 w-14 rounded-lg border border-slate-300"
              />
              <input
                placeholder="#0f172a"
                value={actual.colorSecundario ?? ''}
                onChange={(e) => cambiar('colorSecundario', e.target.value || null)}
                className={entradaClase}
              />
            </div>
          </Campo>

          <Campo etiqueta="WhatsApp" errores={errores.Whatsapp}>
            <input
              placeholder="+59899123456"
              value={actual.whatsapp ?? ''}
              onChange={(e) => cambiar('whatsapp', e.target.value || null)}
              className={entradaClase}
            />
          </Campo>

          <Campo etiqueta="Teléfono" errores={errores.Telefono}>
            <input
              placeholder="+59824001234"
              value={actual.telefono ?? ''}
              onChange={(e) => cambiar('telefono', e.target.value || null)}
              className={entradaClase}
            />
          </Campo>
        </div>

        <Campo etiqueta="Dirección" errores={errores.Direccion}>
          <input
            value={actual.direccion ?? ''}
            onChange={(e) => cambiar('direccion', e.target.value || null)}
            className={entradaClase}
          />
        </Campo>

        <div className="flex items-center gap-3">
          <button
            type="submit"
            disabled={guardando}
            className="rounded-lg bg-emerald-600 px-5 py-2.5 font-semibold text-white hover:bg-emerald-500 disabled:opacity-50"
          >
            {guardando ? 'Guardando…' : 'Guardar'}
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
