import { Suspense, useEffect, useLayoutEffect, useRef, useState } from 'react'
import { Outlet, useLocation, useNavigationType, useParams } from 'react-router-dom'
import { api, ApiError } from '@shared/api/client'
import { Estado } from '@shared/ui/Estado'
import type { TenantPublico } from '@shared/api/types'
import { Cabecera } from '@public/Cabecera'
import { Pie } from '@public/Pie'
import { TenantContexto, variablesDeMarca } from '@public/TenantContexto'

type Carga =
  | { tipo: 'cargando' }
  | { tipo: 'ok'; tenant: TenantPublico }
  | { tipo: 'sin-automotora' }
  | { tipo: 'mantenimiento'; automotora: string | null }
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

  useArrancarArriba()

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

        // Suspendida por falta de pago: mantenimiento, nunca un error. La dirección es de
        // la automotora y lo que ve su cliente también es su reputación.
        if (problema instanceof ApiError && problema.problem?.type === 'sitio-en-mantenimiento') {
          setCarga({ tipo: 'mantenimiento', automotora: problema.problem.automotora ?? null })
          return
        }

        setCarga({
          tipo: 'error',
          mensaje: problema instanceof Error ? problema.message : 'No se pudo contactar la API.',
        })
      })

    return () => controlador.abort()
  }, [slug])

  const tenant = carga.tipo === 'ok' ? carga.tenant : null

  // De layout y no un efecto común: con un efecto, el primer cuadro saldría con el color
  // del producto y después saltaría al de la automotora.
  useLayoutEffect(() => {
    if (!tenant) return

    const raiz = document.documentElement
    const variables = Object.entries(variablesDeMarca(tenant))

    for (const [nombre, valor] of variables) raiz.style.setProperty(nombre, valor)

    return () => {
      for (const [nombre] of variables) raiz.style.removeProperty(nombre)
    }
  }, [tenant])

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
            : import.meta.env.DEV
              ? 'Esta dirección no corresponde a ninguna automotora publicada. En desarrollo, entrá por /t/{slug}.'
              : 'Esta dirección no corresponde a ninguna automotora publicada.'
        }
      />
    )
  }

  if (carga.tipo === 'mantenimiento') {
    return (
      <Estado
        titulo={carga.automotora ?? 'Sitio en mantenimiento'}
        detalle="Estamos haciendo mejoras en el sitio. Volvé a visitarnos en unos días."
      />
    )
  }

  if (carga.tipo === 'error') {
    return <Estado titulo="Algo salió mal" detalle={carga.mensaje} />
  }

  const base = slug ? `/t/${slug}` : ''

  return (
    <TenantContexto.Provider value={{ tenant: carga.tenant, slug }}>
      <div className="flex min-h-screen flex-col bg-white">
        <Cabecera tenant={carga.tenant} base={base} />

        <main className="flex-1">
          {/* Mientras llega una página diferida, la cabecera y el pie se quedan: solo el
              medio espera, con su alto, para que el pie no suba y vuelva a bajar. */}
          <Suspense fallback={<div className="min-h-screen" />}>
            <Outlet />
          </Suspense>
        </main>

        <Pie tenant={carga.tenant} base={base} />
      </div>
    </TenantContexto.Provider>
  )
}

/**
 * Cada página nueva arranca arriba. Volver atrás no: ahí el navegador recupera la posición
 * en la que estaba la persona, que es lo que espera.
 *
 * Es un efecto de layout para que corra antes de que el navegador saque la foto de la
 * página nueva en la transición.
 */
function useArrancarArriba() {
  const { pathname } = useLocation()
  const tipo = useNavigationType()
  const rutaAnterior = useRef<string | null>(null)

  // Cambiar un filtro reemplaza la URL sin cambiar de página: eso no es una página nueva,
  // y sin esta comparación cada filtro mandaría arriba de todo.
  useLayoutEffect(() => {
    if (rutaAnterior.current === pathname) return
    rutaAnterior.current = pathname

    if (tipo !== 'POP') window.scrollTo(0, 0)
  }, [pathname, tipo])
}
