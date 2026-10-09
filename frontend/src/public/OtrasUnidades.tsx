import { useEffect, useMemo, useState } from 'react'
import { api } from '@shared/api/client'
import type { HomePublica, VehiculoPublico, VehiculoPublicoResumen } from '@shared/api/types'
import { CarruselDeVehiculos } from '@public/CarruselDeVehiculos'
import { useSitio } from '@public/TenantContexto'
import { Aparecer } from '@public/ui/Aparecer'

const MAXIMO = 8

/**
 * Otras unidades del stock, para seguir mirando sin volver al listado.
 *
 * Salen de la home y no del listado a propósito: pedir el listado con un filtro de
 * carrocería registraría una búsqueda que ninguna persona hizo, y los reportes de demanda
 * la contarían. De lo que trae la home van primero las de la misma carrocería y, entre
 * esas, las de precio más parecido.
 */
export function OtrasUnidades({ vehiculo }: { vehiculo: VehiculoPublico }) {
  const { slug } = useSitio()
  const [home, setHome] = useState<HomePublica | null>(null)

  const base = slug ? `/t/${slug}` : ''

  useEffect(() => {
    const controlador = new AbortController()

    api.publico
      .home(slug, controlador.signal)
      .then(setHome)
      .catch(() => undefined)

    return () => controlador.abort()
  }, [slug])

  const otras = useMemo(
    () => (home ? parecidas([...home.destacados, ...home.recientes], vehiculo) : []),
    [home, vehiculo],
  )

  if (otras.length === 0) return null

  return (
    <section className="mt-20 lg:mt-24">
      <Aparecer className="mb-8">
        <h2 className="text-2xl font-bold tracking-tight text-slate-950 sm:text-3xl">
          Otras unidades que te pueden interesar
        </h2>
      </Aparecer>
      <CarruselDeVehiculos vehiculos={otras} base={base} etiqueta="Otras unidades" />
    </section>
  )
}

function parecidas(candidatos: VehiculoPublicoResumen[], vehiculo: VehiculoPublico): VehiculoPublicoResumen[] {
  // Otra carrocería pesa más que cualquier diferencia de precio, y otra moneda más que
  // cualquier diferencia dentro de la misma: no se comparan dólares con pesos.
  const distancia = (otro: VehiculoPublicoResumen) =>
    (otro.carroceria === vehiculo.carroceria ? 0 : 1e12) +
    (otro.moneda === vehiculo.moneda ? Math.abs(otro.precio - vehiculo.precio) : 1e11)

  return candidatos
    .filter((otro) => otro.id !== vehiculo.id)
    .sort((a, b) => distancia(a) - distancia(b))
    .slice(0, MAXIMO)
}
