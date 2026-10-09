import type { ReactNode } from 'react'
import { useEnPantalla } from '@public/ui/movimiento'

interface Props {
  children: ReactNode
  /** Segundos de espera, para escalonar los elementos de una misma fila. */
  retraso?: number
  className?: string
}

/**
 * Aparece subiendo cuando entra en pantalla, una sola vez.
 *
 * Con movimiento reducido no sube: solo se funde, que no marea a nadie.
 */
export function Aparecer({ children, retraso = 0, className = '' }: Props) {
  const [ref, visto] = useEnPantalla<HTMLDivElement>()

  return (
    <div
      ref={ref}
      style={{ transitionDelay: `${retraso}s` }}
      className={`transition-[opacity,translate] duration-700 ease-salida ${
        visto ? 'opacity-100' : 'translate-y-7 opacity-0 motion-reduce:translate-y-0'
      } ${className}`}
    >
      {children}
    </div>
  )
}
