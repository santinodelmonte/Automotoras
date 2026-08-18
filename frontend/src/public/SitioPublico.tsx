import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { api, ApiError } from '@shared/api/client'
import type { TenantPublico } from '@shared/api/types'

type Estado =
  | { tipo: 'cargando' }
  | { tipo: 'ok'; tenant: TenantPublico }
  | { tipo: 'sin-automotora' }
  | { tipo: 'error'; mensaje: string }

/**
 * Sitio público de una automotora.
 *
 * El tenant no se elige acá: se resuelve en el servidor desde el dominio o desde el slug
 * de la ruta, y siempre contra la tabla de automotoras. Si no matchea, 404 — no existe
 * una automotora por defecto.
 */
export function SitioPublico() {
  const { slug = null } = useParams<{ slug: string }>()
  const [estado, setEstado] = useState<Estado>({ tipo: 'cargando' })

  useEffect(() => {
    const controlador = new AbortController()

    api.publico
      .tenant(slug, controlador.signal)
      .then((tenant) => setEstado({ tipo: 'ok', tenant }))
      .catch((problema: unknown) => {
        if (controlador.signal.aborted) return

        if (problema instanceof ApiError && problema.status === 404) {
          setEstado({ tipo: 'sin-automotora' })
          return
        }

        setEstado({
          tipo: 'error',
          mensaje: problema instanceof Error ? problema.message : 'No se pudo contactar la API.',
        })
      })

    return () => controlador.abort()
  }, [slug])

  if (estado.tipo === 'cargando') {
    return <Pantalla titulo="Cargando…" detalle="Buscando la automotora." />
  }

  if (estado.tipo === 'sin-automotora') {
    return (
      <Pantalla
        titulo="No encontramos esta automotora"
        detalle="La dirección no corresponde a ninguna automotora publicada."
      />
    )
  }

  if (estado.tipo === 'error') {
    return <Pantalla titulo="Algo salió mal" detalle={estado.mensaje} />
  }

  const { tenant } = estado
  const primario = tenant.colorPrimario ?? '#059669'

  return (
    <main className="min-h-screen bg-white text-slate-900">
      <header className="border-b border-slate-200 p-6" style={{ borderTopColor: primario, borderTopWidth: 6 }}>
        <div className="mx-auto flex max-w-4xl items-center gap-4">
          {tenant.logoUrl && <img src={tenant.logoUrl} alt={tenant.nombre} className="h-10 w-auto" />}
          <div>
            <h1 className="text-2xl font-bold" style={{ color: primario }}>
              {tenant.nombre}
            </h1>
            {tenant.direccion && <p className="text-sm text-slate-500">{tenant.direccion}</p>}
          </div>
        </div>
      </header>

      <section className="mx-auto max-w-4xl p-6">
        <div className="flex flex-wrap gap-3">
          {tenant.whatsapp && (
            <a
              href={`https://wa.me/${tenant.whatsapp.replace(/\D/g, '')}`}
              className="rounded-lg px-4 py-2 font-semibold text-white"
              style={{ backgroundColor: primario }}
            >
              WhatsApp
            </a>
          )}
          {tenant.telefono && (
            <a
              href={`tel:${tenant.telefono}`}
              className="rounded-lg border border-slate-300 px-4 py-2 font-semibold text-slate-700"
            >
              {tenant.telefono}
            </a>
          )}
        </div>

        <p className="mt-8 text-slate-500">
          El catálogo, los filtros y las fichas de vehículos llegan con las features de fase 1.
          Lo que ya funciona es lo que iba primero: la automotora se resuelve sola, por su
          dominio o por su slug, y con su identidad.
        </p>
      </section>
    </main>
  )
}

function Pantalla({ titulo, detalle }: { titulo: string; detalle: string }) {
  return (
    <main className="flex min-h-screen items-center justify-center bg-white p-6 text-center">
      <div>
        <h1 className="text-2xl font-bold text-slate-900">{titulo}</h1>
        <p className="mt-2 text-slate-500">{detalle}</p>
      </div>
    </main>
  )
}
