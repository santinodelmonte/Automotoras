interface Props {
  titulo: string
  detalle?: string
  children?: React.ReactNode
}

/** Pantalla completa para cargando, vacío o error. */
export function Estado({ titulo, detalle, children }: Props) {
  return (
    <div className="flex min-h-[50vh] flex-col items-center justify-center gap-3 p-8 text-center">
      <p className="text-lg font-semibold text-slate-900 dark:text-slate-100">{titulo}</p>
      {detalle && <p className="max-w-md text-sm text-slate-500">{detalle}</p>}
      {children}
    </div>
  )
}

/** Bloque de carga con la forma aproximada del contenido que va a venir. */
export function Esqueleto({ className = '' }: { className?: string }) {
  return <div className={`animate-pulse rounded-lg bg-slate-200 dark:bg-slate-800 ${className}`} />
}
