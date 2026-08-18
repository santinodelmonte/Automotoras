import { useEffect, useState } from 'react'
import { Link, Outlet, useParams } from 'react-router-dom'
import { api, ApiError } from '@shared/api/client'
import { Estado } from '@shared/ui/Estado'
import type { TenantPublico } from '@shared/api/types'
import { TenantContexto, colorPrimario } from '@public/TenantContexto'

type Carga =
  | { tipo: 'cargando' }
  | { tipo: 'ok'; tenant: TenantPublico }
  | { tipo: 'sin-automotora' }
  | { tipo: 'error'; mensaje: string }

/**
 * Cabecera, pie y branding del sitio público.
 *
 * La automotora la resuelve el servidor —por el dominio propio o por el slug de la ruta—
 * y siempre contra la tabla. Acá no se elige nada: si el servidor dice que no hay, se
 * muestra que no hay.
 */
export function SitioPublicoLayout() {
  const { slug = null } = useParams<{ slug: string }>()
  const [carga, setCarga] = useState<Carga>({ tipo: 'cargando' })

  useEffect(() => {
    const controlador = new AbortController()
    setCarga({ tipo: 'cargando' })

    api.publico
      .tenant(slug, controlador.signal)
      .then((tenant) => setCarga({ tipo: 'ok', tenant }))
      .catch((problema: unknown) => {
        if (controlador.signal.aborted) return

        if (problema instanceof ApiError && problema.status === 404) {
          setCarga({ tipo: 'sin-automotora' })
          return
        }

        setCarga({
          tipo: 'error',
          mensaje: problema instanceof Error ? problema.message : 'No se pudo contactar la API.',
        })
      })

    return () => controlador.abort()
  }, [slug])

  if (carga.tipo === 'cargando') {
    return <Estado titulo="Cargando…" />
  }

  if (carga.tipo === 'sin-automotora') {
    return (
      <Estado
        titulo="No encontramos esta automotora"
        detalle={
          slug
            ? `No hay ninguna automotora publicada con el slug "${slug}".`
            : 'Esta dirección no corresponde a ninguna automotora publicada. En desarrollo, entrá por /t/{slug}.'
        }
      />
    )
  }

  if (carga.tipo === 'error') {
    return <Estado titulo="Algo salió mal" detalle={carga.mensaje} />
  }

  const { tenant } = carga
  const primario = colorPrimario(tenant)
  const base = slug ? `/t/${slug}` : ''

  return (
    <TenantContexto.Provider value={{ tenant, slug }}>
      <div className="flex min-h-screen flex-col bg-slate-50 text-slate-900">
        <header className="border-b border-slate-200 bg-white" style={{ borderTopColor: primario, borderTopWidth: 4 }}>
          <div className="mx-auto flex max-w-6xl items-center justify-between gap-4 px-4 py-4">
            <Link to={base || '/'} className="flex items-center gap-3">
              {tenant.logoUrl ? (
                <img src={tenant.logoUrl} alt={tenant.nombre} className="h-10 w-auto" />
              ) : (
                <span
                  className="grid h-10 w-10 place-items-center rounded-lg font-bold text-white"
                  style={{ backgroundColor: primario }}
                >
                  {tenant.nombre.charAt(0)}
                </span>
              )}
              <span className="text-lg font-bold">{tenant.nombre}</span>
            </Link>

            <nav className="flex items-center gap-4 text-sm font-medium">
              <Link to={`${base}/vehiculos`} className="hover:underline">
                Vehículos
              </Link>
              {tenant.telefono && (
                <a href={`tel:${tenant.telefono}`} className="hidden sm:inline hover:underline">
                  {tenant.telefono}
                </a>
              )}
            </nav>
          </div>
        </header>

        <main className="mx-auto w-full max-w-6xl flex-1 px-4 py-6">
          <Outlet />
        </main>

        <footer className="border-t border-slate-200 bg-white">
          <div className="mx-auto flex max-w-6xl flex-col gap-1 px-4 py-6 text-sm text-slate-500">
            <p className="font-semibold text-slate-700">{tenant.nombre}</p>
            {tenant.direccion && <p>{tenant.direccion}</p>}
            {tenant.telefono && <p>Tel. {tenant.telefono}</p>}
          </div>
        </footer>
      </div>
    </TenantContexto.Provider>
  )
}
