import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '@shared/api/client'
import { Esqueleto, Estado } from '@shared/ui/Estado'
import { entero } from '@shared/ui/formato'
import { colorPrimario, useSitio } from '@public/TenantContexto'
import { VehiculoCard } from '@public/VehiculoCard'
import type { HomePublica } from '@shared/api/types'

export function HomePage() {
  const { tenant, slug } = useSitio()
  const [home, setHome] = useState<HomePublica | null>(null)
  const [error, setError] = useState<string | null>(null)

  const base = slug ? `/t/${slug}` : ''
  const primario = colorPrimario(tenant)

  useEffect(() => {
    const controlador = new AbortController()

    api.publico
      .home(slug, controlador.signal)
      .then(setHome)
      .catch((problema: unknown) => {
        if (controlador.signal.aborted) return
        setError(problema instanceof Error ? problema.message : 'No se pudo cargar el catálogo.')
      })

    return () => controlador.abort()
  }, [slug])

  useEffect(() => {
    document.title = tenant.nombre
  }, [tenant.nombre])

  if (error) return <Estado titulo="No pudimos cargar el catálogo" detalle={error} />

  if (!home) {
    return (
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {[0, 1, 2, 3, 4, 5].map((i) => (
          <Esqueleto key={i} className="h-72" />
        ))}
      </div>
    )
  }

  if (home.totalDisponibles === 0) {
    return (
      <Estado
        titulo="Todavía no hay vehículos publicados"
        detalle="Escribinos y contanos qué estás buscando."
      />
    )
  }

  return (
    <div className="flex flex-col gap-10">
      <section
        className="rounded-2xl px-6 py-10 text-white"
        style={{ backgroundColor: primario }}
      >
        <h1 className="text-3xl font-bold">{tenant.nombre}</h1>
        <p className="mt-2 max-w-lg text-white/80">
          {entero(home.totalDisponibles)} vehículos disponibles. Filtrá por marca, año, precio y
          kilometraje.
        </p>
        <Link
          to={`${base}/vehiculos`}
          className="mt-6 inline-block rounded-lg bg-white px-5 py-2.5 font-semibold"
          style={{ color: primario }}
        >
          Ver todos
        </Link>
      </section>

      {home.destacados.length > 0 && (
        <Grilla titulo="Destacados" base={base} primario={primario} vehiculos={home.destacados} />
      )}

      {home.recientes.length > 0 && (
        <Grilla
          titulo="Últimos ingresos"
          base={base}
          primario={primario}
          vehiculos={home.recientes}
        />
      )}
    </div>
  )
}

function Grilla({
  titulo,
  base,
  primario,
  vehiculos,
}: {
  titulo: string
  base: string
  primario: string
  vehiculos: HomePublica['destacados']
}) {
  return (
    <section>
      <h2 className="mb-4 text-xl font-bold">{titulo}</h2>
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {vehiculos.map((vehiculo) => (
          <VehiculoCard
            key={vehiculo.id}
            vehiculo={vehiculo}
            base={base}
            colorPrimario={primario}
          />
        ))}
      </div>
    </section>
  )
}
