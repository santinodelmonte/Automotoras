import { useEffect, useId, useMemo, useRef, useState, type KeyboardEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { Search } from 'lucide-react'
import { api } from '@shared/api/client'
import { idDeVisita } from '@shared/analitica/sesion'
import { linkDeWhatsapp } from '@shared/ui/formato'
import type { FiltrosDisponibles, MarcaConStock } from '@shared/api/types'
import { useSitio } from '@public/TenantContexto'
import { IconoWhatsapp } from '@public/ui/IconoWhatsapp'

/** Manda la búsqueda una sola vez por texto y por tipo dentro de la visita. */
function registrarBusqueda(slug: string | null, texto: string, confirmada: boolean, registradas: Set<string>) {
  const clave = `${confirmada}:${texto.toLowerCase()}`
  if (registradas.has(clave)) return

  registradas.add(clave)
  void api.publico.busqueda(slug, { texto, confirmada, sessionId: idDeVisita() })
}

interface Sugerencia {
  clave: string
  texto: string
  detalle: string
  marcaId: number
  modeloId?: number
}

const MAXIMO_DE_SUGERENCIAS = 8

/** Cuánto tiene que quedarse quieto el texto para contarlo como búsqueda y no como tipeo. */
const ESPERA_PARA_REGISTRAR_MS = 1500

/**
 * Buscador de la portada: sugiere marcas y modelos mientras se escribe.
 *
 * Sugiere solo lo que la automotora tiene publicado, que es lo que traen los filtros: un
 * buscador que ofrece un modelo para después mostrar cero resultados es peor que no tener
 * buscador. Lo elegido se convierte en los filtros de siempre del listado, así que la
 * búsqueda queda registrada como cualquier otra.
 *
 * Lo que no tiene sugerencias no llega al listado, así que se registra acá: es la demanda
 * de lo que la automotora no tiene, la señal que más le importa al dueño. Se manda cuando
 * el texto se queda quieto un momento —el servidor lo guarda solo si reconoce una marca o
 * un modelo— y otra vez, confirmada, si se aprieta "Buscar" o se toca el WhatsApp.
 */
export function Buscador({ filtros }: { filtros: FiltrosDisponibles | null }) {
  const { tenant, slug } = useSitio()
  const navegar = useNavigate()
  const id = useId()

  const [texto, setTexto] = useState('')
  const [abierto, setAbierto] = useState(false)
  const [activa, setActiva] = useState(0)

  const sugerencias = useMemo(() => sugerir(filtros?.marcas ?? [], texto), [filtros, texto])
  const buscado = texto.trim()
  const mostrar = abierto && buscado !== '' && filtros !== null
  const base = slug ? `/t/${slug}` : ''
  const sinCoincidencias = filtros !== null && buscado.length >= 2 && sugerencias.length === 0

  // Lo ya mandado, para no registrar dos veces lo mismo en la misma visita.
  const registradas = useRef(new Set<string>())

  function registrar(confirmada: boolean) {
    if (sinCoincidencias) registrarBusqueda(slug, buscado, confirmada, registradas.current)
  }

  useEffect(() => {
    if (!sinCoincidencias) return

    const temporizador = window.setTimeout(
      () => registrarBusqueda(slug, buscado, false, registradas.current),
      ESPERA_PARA_REGISTRAR_MS,
    )
    return () => window.clearTimeout(temporizador)
  }, [sinCoincidencias, buscado, slug])

  function ir(sugerencia: Sugerencia | undefined) {
    if (buscado === '') {
      navegar(`${base}/vehiculos`, { viewTransition: true })
      return
    }

    // Sin coincidencias no se navega: queda a la vista el aviso con el WhatsApp.
    if (!sugerencia) {
      registrar(true)
      return
    }

    const parametros = new URLSearchParams({ marcaId: String(sugerencia.marcaId) })
    if (sugerencia.modeloId) parametros.set('modeloId', String(sugerencia.modeloId))

    navegar(`${base}/vehiculos?${parametros}`, { viewTransition: true })
  }

  function alTeclear(evento: KeyboardEvent<HTMLInputElement>) {
    if (evento.key === 'ArrowDown') {
      evento.preventDefault()
      setAbierto(true)
      setActiva((actual) => Math.min(actual + 1, Math.max(sugerencias.length - 1, 0)))
    } else if (evento.key === 'ArrowUp') {
      evento.preventDefault()
      setActiva((actual) => Math.max(actual - 1, 0))
    } else if (evento.key === 'Escape') {
      setAbierto(false)
    }
  }

  return (
    <div className="relative">
      <form
        role="search"
        onSubmit={(evento) => {
          evento.preventDefault()
          ir(sugerencias[activa])
        }}
        className="flex items-center gap-2 rounded-2xl bg-white p-2 shadow-elevada ring-1 ring-slate-900/5 transition focus-within:ring-2 focus-within:ring-marca"
      >
        <Search className="ml-2 size-5 shrink-0 text-slate-400" aria-hidden />
        <input
          type="search"
          role="combobox"
          aria-label="Buscar por marca o modelo"
          aria-expanded={mostrar && sugerencias.length > 0}
          aria-controls={`${id}-sugerencias`}
          aria-autocomplete="list"
          aria-activedescendant={
            mostrar && sugerencias[activa] ? `${id}-${sugerencias[activa].clave}` : undefined
          }
          autoComplete="off"
          value={texto}
          onChange={(evento) => {
            setTexto(evento.target.value)
            setActiva(0)
            setAbierto(true)
          }}
          onFocus={() => setAbierto(true)}
          onBlur={() => setAbierto(false)}
          onKeyDown={alTeclear}
          placeholder="Buscá por marca o modelo"
          className="min-w-0 flex-1 bg-transparent py-2 text-base text-slate-900 outline-none placeholder:text-slate-400 [&::-webkit-search-cancel-button]:hidden"
        />
        <button
          type="submit"
          className="shrink-0 rounded-xl bg-marca px-4 py-2.5 text-sm font-semibold text-marca-contraste transition hover:brightness-110 active:scale-[0.98] sm:px-6"
        >
          Buscar
        </button>
      </form>

      {mostrar && sugerencias.length > 0 && (
        <ul
          id={`${id}-sugerencias`}
          role="listbox"
          aria-label="Sugerencias"
          className="absolute inset-x-0 top-full z-30 mt-2 max-h-80 overflow-auto rounded-2xl bg-white p-2 text-left shadow-elevada ring-1 ring-slate-900/5 motion-safe:animate-aparecer"
        >
          {sugerencias.map((sugerencia, indice) => (
            <li
              key={sugerencia.clave}
              id={`${id}-${sugerencia.clave}`}
              role="option"
              aria-selected={indice === activa}
              // Sin esto, el clic le saca el foco al campo, la lista se cierra y el clic
              // termina cayendo en lo que había debajo.
              onMouseDown={(evento) => evento.preventDefault()}
              onClick={() => ir(sugerencia)}
              onMouseEnter={() => setActiva(indice)}
              className={`flex cursor-pointer items-center justify-between gap-3 rounded-xl px-3 py-2.5 text-sm ${
                indice === activa ? 'bg-slate-100' : ''
              }`}
            >
              <span className="font-medium text-slate-900">{sugerencia.texto}</span>
              <span className="shrink-0 text-xs text-slate-500">{sugerencia.detalle}</span>
            </li>
          ))}
        </ul>
      )}

      {mostrar && sugerencias.length === 0 && (
        <div
          role="status"
          onMouseDown={(evento) => evento.preventDefault()}
          className="absolute inset-x-0 top-full z-30 mt-2 rounded-2xl bg-white p-4 text-left text-sm text-slate-600 shadow-elevada ring-1 ring-slate-900/5 motion-safe:animate-aparecer"
        >
          <p>Ahora no tenemos “{buscado}” publicado.</p>
          {tenant.whatsapp && (
            <a
              href={linkDeWhatsapp(tenant.whatsapp, `Hola, estoy buscando ${buscado}. ¿Les entra algo así?`)}
              target="_blank"
              rel="noreferrer"
              onClick={() => registrar(true)}
              className="mt-2 inline-flex items-center gap-2 font-semibold text-emerald-700 hover:underline"
            >
              <IconoWhatsapp className="size-4" />
              Escribinos y te avisamos cuando entre
            </a>
          )}
        </div>
      )}
    </div>
  )
}

/** Sin mayúsculas ni tildes: "citroen" tiene que encontrar "Citroën". */
function normalizar(texto: string): string {
  return texto
    .normalize('NFD')
    .replace(/\p{Diacritic}/gu, '')
    .toLowerCase()
    .trim()
}

/**
 * Marcas y modelos que contienen todas las palabras escritas, en cualquier orden: "hilux
 * toyota" y "toy hil" encuentran lo mismo.
 */
function sugerir(marcas: MarcaConStock[], texto: string): Sugerencia[] {
  const palabras = normalizar(texto).split(/\s+/).filter(Boolean)
  if (palabras.length === 0) return []

  const coincide = (nombre: string) => palabras.every((palabra) => nombre.includes(palabra))
  const sugerencias: Sugerencia[] = []

  for (const marca of marcas) {
    const nombreDeMarca = normalizar(marca.nombre)

    if (coincide(nombreDeMarca)) {
      sugerencias.push({
        clave: `marca-${marca.id}`,
        texto: marca.nombre,
        detalle: 'Todos los modelos',
        marcaId: marca.id,
      })
    }

    for (const modelo of marca.modelos) {
      if (coincide(`${nombreDeMarca} ${normalizar(modelo.nombre)}`)) {
        sugerencias.push({
          clave: `modelo-${modelo.id}`,
          texto: `${marca.nombre} ${modelo.nombre}`,
          detalle: marca.nombre,
          marcaId: marca.id,
          modeloId: modelo.id,
        })
      }
    }
  }

  return sugerencias.slice(0, MAXIMO_DE_SUGERENCIAS)
}
