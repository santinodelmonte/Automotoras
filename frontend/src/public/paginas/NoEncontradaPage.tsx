import { useEffect } from 'react'
import { Link } from 'react-router-dom'
import { Compass } from 'lucide-react'
import { useSitio } from '@public/TenantContexto'

/**
 * Una dirección del sitio que no existe. Antes mandaba a la portada sin decir nada, y
 * quien llegaba por un link viejo no entendía por qué estaba ahí. El servidor ya respondió
 * 404 —para que Google no la indexe—; acá se explica y se ofrece por dónde seguir.
 */
export function NoEncontradaPage() {
  const { tenant, slug } = useSitio()
  const base = slug ? `/t/${slug}` : ''

  useEffect(() => {
    document.title = `Página no encontrada — ${tenant.nombre}`
  }, [tenant.nombre])

  return (
    <div className="mx-auto flex max-w-xl flex-col items-center px-4 py-24 text-center sm:px-6">
      <span className="grid size-14 place-items-center rounded-2xl bg-slate-50 text-slate-500 ring-1 ring-slate-900/5">
        <Compass className="size-7" aria-hidden />
      </span>
      <h1 className="mt-6 text-2xl font-bold text-slate-900">No encontramos esta página</h1>
      <p className="mt-3 text-slate-600">
        Puede que el link esté incompleto o que la página ya no exista. El stock completo de{' '}
        {tenant.nombre} sigue acá.
      </p>

      <div className="mt-8 flex flex-wrap justify-center gap-3">
        <Link
          to={`${base}/vehiculos`}
          viewTransition
          className="rounded-full bg-marca px-5 py-2.5 text-sm font-semibold text-marca-contraste"
        >
          Ver vehículos
        </Link>
        <Link
          to={base || '/'}
          viewTransition
          className="rounded-full bg-white px-5 py-2.5 text-sm font-semibold text-slate-900 ring-1 ring-slate-200 transition hover:ring-slate-400"
        >
          Ir al inicio
        </Link>
      </div>
    </div>
  )
}
