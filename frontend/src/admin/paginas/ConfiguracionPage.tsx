import { useEffect, useRef, useState } from 'react'
import { ImageOff } from 'lucide-react'
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
      <header>
        <h1 className="panel-titulo">Configuración</h1>
        <p className="mt-1 max-w-2xl panel-ayuda">Cómo se ve tu sitio y cómo te contactan los compradores.</p>
      </header>

      <section className="panel-seccion">
        <h2 className="panel-seccion-titulo">Dirección del sitio</h2>
        <p className="mt-1 text-sm text-slate-500">
          El slug y el dominio propio los administra el equipo del SaaS: cambiarlos apaga la
          dirección por la que tu sitio ya está circulando, y hay que coordinar el DNS.
        </p>

        <dl className="mt-3 grid gap-2 text-sm sm:grid-cols-2">
          <div>
            <dt className="text-slate-500">Slug</dt>
            <dd className="font-mono">/t/{actual.slug}</dd>
          </div>
          <div>
            <dt className="text-slate-500">Dominio propio</dt>
            <dd className="font-mono">{actual.dominioCustom ?? '—'}</dd>
          </div>
        </dl>
      </section>

      <section className="panel-seccion">
        <div className="flex items-center gap-4">
          {/* Sin logo también se muestra la caja: si no, no se entiende qué se está cambiando. */}
          <div className="grid h-14 w-20 shrink-0 place-items-center overflow-hidden rounded-xl border border-slate-200 bg-slate-50 p-2 sm:h-16 sm:w-28">
            {actual.logoUrl ? (
              <img src={actual.logoUrl} alt="Logo actual" className="max-h-full max-w-full object-contain" />
            ) : (
              <span className="flex flex-col items-center gap-1 text-xs text-slate-400">
                <ImageOff aria-hidden className="size-4" />
                Sin logo
              </span>
            )}
          </div>

          <div className="min-w-0 flex-1">
            <h2 className="panel-seccion-titulo">Logo</h2>
            <p className="text-sm text-slate-500">Se achica solo antes de subir.</p>
          </div>

          <label className="cursor-pointer panel-boton-secundario">
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
      </section>

      <form onSubmit={guardar} className="flex flex-col gap-4 panel-seccion">
        <div>
          <h2 className="panel-seccion-titulo">Marca y contacto</h2>
          <p className="mt-1 text-sm text-slate-500">
            Los colores pintan tu sitio; el WhatsApp y el teléfono son por donde te escriben los compradores.
          </p>
        </div>

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
                className="h-[38px] w-14 shrink-0 cursor-pointer rounded-lg border border-slate-300 bg-white p-1"
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
                className="h-[38px] w-14 shrink-0 cursor-pointer rounded-lg border border-slate-300 bg-white p-1"
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
            className="panel-boton"
          >
            {guardando ? 'Guardando…' : 'Guardar'}
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
