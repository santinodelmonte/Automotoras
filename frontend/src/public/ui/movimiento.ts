import { useEffect, useRef, useState } from 'react'

/**
 * Si la persona pidió menos movimiento en su sistema. Lo que se anima con CSS lo resuelve
 * la variante `motion-safe:`; esto es para lo que se anima desde JavaScript.
 */
export function prefiereMenosMovimiento(): boolean {
  return window.matchMedia('(prefers-reduced-motion: reduce)').matches
}

/**
 * Si el elemento ya entró en pantalla. Una vez que entra queda en `true`: lo que aparece
 * al bajar aparece una sola vez, no cada vez que se vuelve a pasar por ahí.
 */
export function useEnPantalla<T extends Element>(margen = '0px 0px -8% 0px') {
  const ref = useRef<T>(null)
  const [visto, setVisto] = useState(false)

  useEffect(() => {
    const elemento = ref.current
    if (!elemento || visto) return

    const observador = new IntersectionObserver(
      ([entrada]) => {
        if (entrada?.isIntersecting) {
          setVisto(true)
          observador.disconnect()
        }
      },
      { rootMargin: margen },
    )

    observador.observe(elemento)
    return () => observador.disconnect()
  }, [visto, margen])

  return [ref, visto] as const
}

/**
 * Cuánto se bajó a través del elemento, de 0 a 1, en la variable CSS `--progreso`.
 *
 * Se escribe directo en el estilo y no en el estado de React: cambia en cada cuadro del
 * scroll, y repintar componentes a ese ritmo trabaría justo lo que se quiere suave. Las
 * lecturas se juntan en un cuadro de animación por vez.
 */
export function useProgresoDeScroll<T extends HTMLElement>() {
  const ref = useRef<T>(null)

  useEffect(() => {
    const elemento = ref.current
    if (!elemento || prefiereMenosMovimiento()) return

    let cuadro = 0

    const medir = () => {
      cuadro = 0
      const { top, height } = elemento.getBoundingClientRect()
      const progreso = Math.min(Math.max(-top / height, 0), 1)
      elemento.style.setProperty('--progreso', progreso.toFixed(3))
    }

    const alBajar = () => {
      if (!cuadro) cuadro = requestAnimationFrame(medir)
    }

    medir()
    window.addEventListener('scroll', alBajar, { passive: true })

    return () => {
      window.removeEventListener('scroll', alBajar)
      cancelAnimationFrame(cuadro)
    }
  }, [])

  return ref
}
