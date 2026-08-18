interface Props {
  titulo: string
  valor: string
  nota?: string
}

/**
 * Una cifra grande con su rótulo. La usan el tablero y los reportes.
 *
 * Vive en `shared` y no en cada pantalla porque son las dos caras del mismo dato: si el
 * tablero y el reporte de demanda muestran las vistas con tipografías distintas, el dueño
 * asume que están midiendo cosas distintas.
 */
export function Tarjeta({ titulo, valor, nota }: Props) {
  return (
    <div className="rounded-xl border border-slate-200 bg-white p-5">
      <p className="text-sm text-slate-500">{titulo}</p>
      <p className="mt-1 text-3xl font-bold">{valor}</p>
      {nota && <p className="mt-1 text-xs text-slate-400">{nota}</p>}
    </div>
  )
}
