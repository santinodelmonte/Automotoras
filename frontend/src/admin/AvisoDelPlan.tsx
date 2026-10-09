import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '@shared/api/client'
import { fecha } from '@shared/ui/formato'
import type { SituacionDelPlan } from '@shared/api/types'

/**
 * Los avisos del plan arriba de todo el panel del dueño: vencimiento y cercanía a los
 * topes.
 *
 * Avisa antes de bloquear. Que el cliente se entere del techo cuando lo choca, o de la
 * deuda cuando se le apaga el sitio un sábado, es la peor forma de enterarse.
 */
export function AvisoDelPlan() {
  const [situacion, setSituacion] = useState<SituacionDelPlan | null>(null)

  useEffect(() => {
    const controlador = new AbortController()

    // Si falla no se muestra nada: un aviso que no se pudo cargar no puede tapar el panel.
    api.tenant
      .plan(controlador.signal)
      .then(setSituacion)
      .catch(() => undefined)

    return () => controlador.abort()
  }, [])

  if (!situacion) return null

  const avisos = armarAvisos(situacion)
  if (avisos.length === 0) return null

  return (
    <div className="mb-6 flex flex-col gap-2">
      {avisos.map((aviso) => (
        <div key={aviso.texto} className={`rounded-xl border px-4 py-3 text-sm ${estilos[aviso.nivel]}`}>
          {aviso.texto}{' '}
          <Link to="/admin/plan" className="font-semibold underline">
            Ver mi plan
          </Link>
        </div>
      ))}
    </div>
  )
}

type Nivel = 'info' | 'alerta' | 'grave'

const estilos: Record<Nivel, string> = {
  info: 'border-sky-200 bg-sky-50 text-sky-900',
  alerta: 'border-amber-300 bg-amber-50 text-amber-900',
  grave: 'border-rose-300 bg-rose-50 text-rose-900',
}

function armarAvisos(situacion: SituacionDelPlan): { nivel: Nivel; texto: string }[] {
  const avisos: { nivel: Nivel; texto: string }[] = []
  const vence = fecha(situacion.pagaHasta)
  const dias = situacion.diasParaVencer ?? 0

  switch (situacion.estado) {
    case 'PorVencer':
      avisos.push({
        nivel: 'info',
        texto:
          dias === 0
            ? `El abono vence hoy (${vence}).`
            : `El abono vence en ${dias} ${dias === 1 ? 'día' : 'días'}, el ${vence}.`,
      })
      break
    case 'Gracia':
      avisos.push({
        nivel: 'alerta',
        texto: `El abono venció el ${vence}. El sitio sigue publicado por unos días más; regularizalo para que no quede en mantenimiento.`,
      })
      break
    case 'Suspendido':
      avisos.push({
        nivel: 'grave',
        texto: situacion.plan
          ? `El abono venció el ${vence} y el sitio público está en mantenimiento. Tus datos están intactos y podés exportarlos.`
          : 'La automotora no tiene un plan vigente y el sitio público está en mantenimiento. Tus datos están intactos y podés exportarlos.',
      })
      break
  }

  const { vehiculos, usuarios } = situacion

  if (vehiculos.tope !== null && vehiculos.usados >= vehiculos.tope) {
    avisos.push({
      nivel: 'alerta',
      texto: `Llegaste al tope de ${vehiculos.tope} vehículos publicados del plan ${situacion.plan?.nombre}. Para publicar más, pausá o vendé alguna unidad, o pasá a un plan más grande.`,
    })
  } else if (vehiculos.cercaDelTope) {
    avisos.push({
      nivel: 'info',
      texto: `Tenés ${vehiculos.usados} de ${vehiculos.tope} vehículos publicados que permite tu plan.`,
    })
  }

  if (usuarios.tope !== null && usuarios.usados >= usuarios.tope) {
    avisos.push({
      nivel: 'info',
      texto: `Usás los ${usuarios.tope} usuarios que incluye tu plan.`,
    })
  }

  return avisos
}
