import { useState } from 'react'
import { Link } from 'react-router-dom'
import { ArrowUpRight, Check, ChevronRight, X } from 'lucide-react'
import type { PrimerosPasos as Datos } from '@shared/api/types'

interface Paso {
  titulo: string
  detalle: string
  hecho: boolean
  ir: string
  accion: string
}

const OCULTO = 'automotora.primeros-pasos-oculto'

/**
 * Lo que le falta a una automotora recién dada de alta para que su sitio salga bien, en el
 * orden en que conviene hacerlo.
 *
 * Cada paso se da por hecho cuando el dato está cargado, no cuando alguien lo marca: la
 * lista no puede decir "listo" con el sitio todavía sin logo. Cuando están todos, la
 * tarjeta desaparece sola. El dueño la puede ocultar antes; queda oculta en ese navegador.
 */
export function PrimerosPasos({ datos }: { datos: Datos }) {
  const clave = `${OCULTO}:${datos.slug}`
  const [oculto, setOculto] = useState(() => leer(clave))

  const sinFotos = datos.publicadosSinFotos
  const pasos: Paso[] = [
    {
      titulo: 'Subí tu logo',
      detalle: 'Aparece en la cabecera del sitio, en la pestaña del navegador y al compartir un link.',
      hecho: datos.tieneLogo,
      ir: '/admin/configuracion',
      accion: 'Ir a Configuración',
    },
    {
      titulo: 'Elegí el color de tu marca',
      detalle: 'Botones, etiquetas y acentos del sitio salen de ese color.',
      hecho: datos.tieneColor,
      ir: '/admin/configuracion',
      accion: 'Ir a Configuración',
    },
    {
      titulo: 'Cargá tu WhatsApp',
      detalle: 'Es por donde te van a llegar casi todas las consultas, con el auto ya escrito en el mensaje.',
      hecho: datos.tieneWhatsapp,
      ir: '/admin/configuracion',
      accion: 'Ir a Configuración',
    },
    {
      titulo: 'Publicá tu primer vehículo',
      detalle: 'Mientras no haya nada disponible, el sitio dice que todavía no hay vehículos publicados.',
      hecho: datos.vehiculosPublicados > 0,
      ir: '/admin/vehiculos/nuevo',
      accion: 'Cargar vehículo',
    },
    {
      titulo: 'Poné fotos a todo lo publicado',
      detalle:
        sinFotos > 0
          ? `${sinFotos === 1 ? 'Hay 1 unidad publicada' : `Hay ${sinFotos} unidades publicadas`} sin fotos. Sin foto casi nadie abre la ficha.`
          : 'Sin foto casi nadie abre la ficha. La primera que subas queda de portada.',
      hecho: datos.vehiculosPublicados > 0 && sinFotos === 0,
      ir: '/admin/vehiculos',
      accion: 'Ver el stock',
    },
    {
      titulo: 'Sumá a tus vendedores',
      detalle: 'Cada uno con su usuario: cargan y actualizan el stock, pero no ven costos ni reportes.',
      hecho: datos.vendedores > 0,
      ir: '/admin/usuarios',
      accion: 'Ir a Usuarios',
    },
  ]

  const hechos = pasos.filter((paso) => paso.hecho).length
  if (oculto || hechos === pasos.length) return null

  const siguiente = pasos.find((paso) => !paso.hecho)
  const sitio = datos.dominioCustom ? `https://${datos.dominioCustom}` : `${window.location.origin}/t/${datos.slug}`

  function ocultar() {
    try {
      localStorage.setItem(clave, '1')
    } catch {
      // Sin almacenamiento se oculta igual, hasta la próxima vez que entre.
    }
    setOculto(true)
  }

  return (
    <section className="panel-seccion border-emerald-100 bg-linear-to-br from-emerald-50/70 via-white to-white">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="panel-seccion-titulo">Primeros pasos</h2>
          <p className="mt-1 panel-ayuda">
            Lo que falta para que tu sitio quede listo para recibir consultas.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <a href={sitio} target="_blank" rel="noreferrer" className="panel-boton-secundario">
            Ver mi sitio
            <ArrowUpRight aria-hidden className="size-4" />
          </a>
          <button
            type="button"
            onClick={ocultar}
            className="grid size-9 place-items-center rounded-lg text-slate-400 transition hover:bg-slate-100 hover:text-slate-700"
            aria-label="Ocultar primeros pasos"
            title="Ocultar"
          >
            <X aria-hidden className="size-4" />
          </button>
        </div>
      </div>

      <div className="mt-5 flex items-center gap-3">
        <div className="h-2 flex-1 overflow-hidden rounded-full bg-slate-100">
          <div
            className="h-full rounded-full bg-emerald-500 transition-[width] duration-500"
            style={{ width: `${(hechos / pasos.length) * 100}%` }}
          />
        </div>
        <span className="shrink-0 text-sm font-medium tabular-nums text-slate-600">
          {hechos} de {pasos.length}
        </span>
      </div>

      <ol className="mt-4 grid gap-2 md:grid-cols-2">
        {pasos.map((paso) => (
          <li key={paso.titulo}>
            {paso.hecho ? (
              <div className="flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm text-slate-500">
                <span className="grid size-6 shrink-0 place-items-center rounded-full bg-emerald-500 text-white">
                  <Check aria-hidden className="size-3.5" strokeWidth={3} />
                </span>
                <span className="line-through decoration-slate-300">{paso.titulo}</span>
              </div>
            ) : (
              <Link
                to={paso.ir}
                className={`group flex items-start gap-3 rounded-xl border px-3 py-2.5 transition hover:border-slate-300 hover:bg-white ${
                  paso === siguiente ? 'border-emerald-200 bg-white shadow-sm' : 'border-slate-200/70'
                }`}
              >
                <span className="mt-0.5 size-6 shrink-0 rounded-full border-2 border-slate-300" aria-hidden />
                <span className="min-w-0 flex-1">
                  <span className="block text-sm font-semibold text-slate-900">{paso.titulo}</span>
                  <span className="mt-0.5 block text-sm text-slate-500">{paso.detalle}</span>
                  <span className="mt-1.5 inline-flex items-center gap-0.5 text-sm font-medium text-emerald-700">
                    {paso.accion}
                    <ChevronRight aria-hidden className="size-4 transition group-hover:translate-x-0.5" />
                  </span>
                </span>
              </Link>
            )}
          </li>
        ))}
      </ol>
    </section>
  )
}

function leer(clave: string) {
  try {
    return localStorage.getItem(clave) === '1'
  } catch {
    return false
  }
}
