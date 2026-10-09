import { Car } from 'lucide-react'

/** El logo del producto. El panel es del SaaS, no de cada automotora: no lleva su color. */
export function Marca({ oscuro = false }: { oscuro?: boolean }) {
  return (
    <div className="flex items-center gap-2.5 px-1">
      <span
        aria-hidden
        className="grid size-8 place-items-center rounded-lg bg-linear-to-br from-emerald-500 to-emerald-700 text-white shadow-[0_2px_6px_rgb(5_150_105/0.35)]"
      >
        <Car className="size-[18px]" />
      </span>
      <span className={`text-[15px] font-semibold tracking-tight ${oscuro ? 'text-white' : 'text-slate-900'}`}>
        Automotora <span className={oscuro ? 'text-emerald-400' : 'text-emerald-600'}>SaaS</span>
      </span>
    </div>
  )
}
