import { useEffect, useState } from 'react'

/** Si la página bajó más de `umbral` píxeles. Para que la cabecera tome fondo al bajar. */
export function useHayScroll(umbral = 0): boolean {
  const [hay, setHay] = useState(() => window.scrollY > umbral)

  useEffect(() => {
    // React descarta el `setState` cuando el valor no cambió, así que escuchar cada evento
    // de scroll no repinta nada hasta que se cruza el umbral.
    const revisar = () => setHay(window.scrollY > umbral)

    revisar()
    window.addEventListener('scroll', revisar, { passive: true })

    return () => window.removeEventListener('scroll', revisar)
  }, [umbral])

  return hay
}
