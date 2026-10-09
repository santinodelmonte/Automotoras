import type { ReactNode } from 'react'

/**
 * El encabezado de cada pantalla del panel: título, una línea que dice para qué sirve y,
 * a la derecha, la acción principal. Todas las pantallas lo arman igual, así el ojo sabe
 * dónde buscar el botón sin leer.
 */
export function Pagina({
  titulo,
  descripcion,
  acciones,
  children,
}: {
  titulo: ReactNode
  descripcion?: ReactNode
  acciones?: ReactNode
  children: ReactNode
}) {
  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="min-w-0">
          <h1 className="panel-titulo">{titulo}</h1>
          {descripcion && <p className="mt-1 max-w-2xl panel-ayuda">{descripcion}</p>}
        </div>
        {acciones && <div className="flex flex-wrap items-center gap-2">{acciones}</div>}
      </header>

      {children}
    </div>
  )
}

/** Una sección del panel con su título y, opcionalmente, una bajada y acciones. */
export function Seccion({
  titulo,
  descripcion,
  acciones,
  children,
  className = '',
}: {
  titulo?: ReactNode
  descripcion?: ReactNode
  acciones?: ReactNode
  children: ReactNode
  className?: string
}) {
  return (
    <section className={`panel-seccion ${className}`}>
      {(titulo || acciones) && (
        <div className="mb-4 flex flex-wrap items-start justify-between gap-3">
          <div>
            {titulo && <h2 className="panel-seccion-titulo">{titulo}</h2>}
            {descripcion && <p className="mt-1 panel-ayuda">{descripcion}</p>}
          </div>
          {acciones}
        </div>
      )}
      {children}
    </section>
  )
}

/**
 * Un campo de formulario con su rótulo y sus errores.
 *
 * El rótulo envuelve al control, así queda asociado sin ids: un lector de pantalla lo
 * anuncia, y tocar el rótulo en el celular enfoca el campo.
 */
export function Campo({
  etiqueta,
  ayuda,
  errores,
  className = '',
  children,
}: {
  etiqueta: string
  ayuda?: string
  errores?: string[]
  className?: string
  children: ReactNode
}) {
  return (
    <label className={`block text-sm ${className}`}>
      <span className="panel-etiqueta">{etiqueta}</span>
      {children}
      {ayuda && !errores?.length && <span className="mt-1.5 block text-xs text-slate-500">{ayuda}</span>}
      {errores?.map((error) => (
        <span key={error} className="panel-error">
          {error}
        </span>
      ))}
    </label>
  )
}

export type Tono = 'verde' | 'ambar' | 'rojo' | 'azul' | 'gris' | 'violeta'

const TONOS: Record<Tono, string> = {
  verde: 'bg-emerald-50 text-emerald-700 ring-emerald-600/20',
  ambar: 'bg-amber-50 text-amber-800 ring-amber-600/25',
  rojo: 'bg-rose-50 text-rose-700 ring-rose-600/20',
  azul: 'bg-sky-50 text-sky-700 ring-sky-600/20',
  gris: 'bg-slate-100 text-slate-600 ring-slate-500/15',
  violeta: 'bg-violet-50 text-violet-700 ring-violet-600/20',
}

const PUNTOS: Record<Tono, string> = {
  verde: 'bg-emerald-500',
  ambar: 'bg-amber-500',
  rojo: 'bg-rose-500',
  azul: 'bg-sky-500',
  gris: 'bg-slate-400',
  violeta: 'bg-violet-500',
}

/** Una etiqueta de estado. El punto de color repite el tono para quien no distingue bien los colores del fondo. */
export function Insignia({ tono, children }: { tono: Tono; children: ReactNode }) {
  return (
    <span className={`panel-insignia ${TONOS[tono]}`}>
      <span aria-hidden className={`size-1.5 rounded-full ${PUNTOS[tono]}`} />
      {children}
    </span>
  )
}

/** Los avisos dentro del panel: información, algo para atender, algo grave. */
export function Aviso({ nivel, children }: { nivel: 'info' | 'alerta' | 'grave' | 'ok'; children: ReactNode }) {
  const estilos = {
    info: 'border-sky-200 bg-sky-50 text-sky-900',
    alerta: 'border-amber-200 bg-amber-50 text-amber-900',
    grave: 'border-rose-200 bg-rose-50 text-rose-900',
    ok: 'border-emerald-200 bg-emerald-50 text-emerald-900',
  }

  return (
    <div role={nivel === 'grave' ? 'alert' : 'status'} className={`rounded-xl border px-4 py-3 text-sm ${estilos[nivel]}`}>
      {children}
    </div>
  )
}
