interface Props {
  titulo: string
  detalle?: string
  children?: React.ReactNode
}

/**
 * Pantalla completa para cargando, vacío o error.
 *
 * Sin variantes `dark:`: la aplicación no tiene modo oscuro, y un título claro sobre el
 * fondo blanco de siempre desaparece en cualquier celular con el modo oscuro activado.
 */
export function Estado({ titulo, detalle, children }: Props) {
  return (
    <div className="flex min-h-[50vh] flex-col items-center justify-center gap-3 p-8 text-center">
      <p className="text-lg font-semibold text-slate-900">{titulo}</p>
      {detalle && <p className="max-w-md text-sm text-slate-500">{detalle}</p>}
      {children}
    </div>
  )
}

/**
 * Bloque de carga con la forma aproximada del contenido que va a venir. El brillo que lo
 * cruza dice "está cargando" sin un spinner; con movimiento reducido queda quieto.
 */
export function Esqueleto({ className = '' }: { className?: string }) {
  return (
    <div
      className={`rounded-lg bg-slate-200/70 bg-[linear-gradient(90deg,transparent_25%,rgb(255_255_255/0.65)_50%,transparent_75%)] bg-[length:200%_100%] bg-no-repeat motion-safe:animate-brillo ${className}`}
    />
  )
}
