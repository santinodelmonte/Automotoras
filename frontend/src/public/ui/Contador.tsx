import { useEffect, useState } from 'react'
import { entero } from '@shared/ui/formato'
import { prefiereMenosMovimiento, useEnPantalla } from '@public/ui/movimiento'

const DURACION = 1400

/**
 * Un número que cuenta desde cero cuando entra en pantalla.
 *
 * La cifra animada es solo para la vista: los lectores de pantalla y los buscadores leen
 * el valor final, que va aparte, y no un cero o un número a mitad de camino.
 */
export function Contador({ valor }: { valor: number }) {
  const [ref, visto] = useEnPantalla<HTMLSpanElement>('0px')
  const [quieto] = useState(prefiereMenosMovimiento)

  useEffect(() => {
    const elemento = ref.current
    if (!elemento || !visto || quieto) return

    // Se escribe directo en el nodo: pasar por el estado de React repintaría el
    // componente sesenta veces por segundo para cambiar un texto.
    const inicio = performance.now()
    let cuadro = 0

    const avanzar = (ahora: number) => {
      const avance = Math.min((ahora - inicio) / DURACION, 1)
      const frenado = 1 - (1 - avance) ** 4
      elemento.textContent = entero(Math.round(valor * frenado))

      if (avance < 1) cuadro = requestAnimationFrame(avanzar)
    }

    cuadro = requestAnimationFrame(avanzar)
    return () => cancelAnimationFrame(cuadro)
  }, [ref, visto, quieto, valor])

  return (
    <span className="tabular-nums">
      <span ref={ref} aria-hidden="true">
        {entero(quieto ? valor : 0)}
      </span>
      <span className="sr-only">{entero(valor)}</span>
    </span>
  )
}
