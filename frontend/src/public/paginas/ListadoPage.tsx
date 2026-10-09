import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { useSearchParams } from 'react-router-dom'
import { Dialog } from 'radix-ui'
import { ChevronLeft, ChevronRight, SearchX, SlidersHorizontal, X } from 'lucide-react'
import { api } from '@shared/api/client'
import { idDeVisita } from '@shared/analitica/sesion'
import { Esqueleto, Estado } from '@shared/ui/Estado'
import { entero, linkDeWhatsapp } from '@shared/ui/formato'
import type {
  FiltrosDisponibles,
  PaginaDe,
  TenantPublico,
  VehiculoPublicoResumen,
} from '@shared/api/types'
import { describirFiltros, leerFiltros, type CambiosDeFiltros, type FiltroActivo } from '@public/filtros'
import { PanelDeFiltros, Selector } from '@public/PanelDeFiltros'
import { useSitio } from '@public/TenantContexto'
import { VehiculoCard } from '@public/VehiculoCard'
import { IconoWhatsapp } from '@public/ui/IconoWhatsapp'
import { useHayScroll } from '@public/ui/useHayScroll'

const ORDENES = [
  { valor: 'precio_asc', texto: 'Precio: menor a mayor' },
  { valor: 'precio_desc', texto: 'Precio: mayor a menor' },
  { valor: 'km_asc', texto: 'Menos kilómetros' },
  { valor: 'anio_desc', texto: 'Más nuevos' },
]

export function ListadoPage() {
  const { tenant, slug } = useSitio()
  const [parametros, setParametros] = useSearchParams()

  const [pagina, setPagina] = useState<PaginaDe<VehiculoPublicoResumen> | null>(null)
  const [cargando, setCargando] = useState(true)
  const [disponibles, setDisponibles] = useState<FiltrosDisponibles | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [hojaAbierta, setHojaAbierta] = useState(false)

  const base = slug ? `/t/${slug}` : ''
  const filtros = useMemo(() => leerFiltros(parametros), [parametros])
  const activos = useMemo(() => describirFiltros(filtros, disponibles), [filtros, disponibles])

  useEffect(() => {
    document.title = `Vehículos — ${tenant.nombre}`
  }, [tenant.nombre])

  // Mientras llega la página nueva queda la anterior, atenuada. Volver al esqueleto en
  // cada filtro hace saltar toda la pantalla por un cambio que la persona acaba de pedir.
  useEffect(() => {
    const controlador = new AbortController()
    setCargando(true)
    setError(null)

    api.publico
      .vehiculos(slug, { ...filtros, sessionId: idDeVisita() ?? undefined }, controlador.signal)
      .then((nueva) => {
        setPagina(nueva)
        setCargando(false)
      })
      .catch((problema: unknown) => {
        if (controlador.signal.aborted) return
        setError(problema instanceof Error ? problema.message : 'No se pudo cargar el listado.')
        setCargando(false)
      })

    return () => controlador.abort()
  }, [slug, filtros])

  // Un solo request trae todo lo filtrable, y los modelos ya vienen adentro de su marca:
  // el select encadenado no necesita ir y volver cada vez que se cambia de marca.
  useEffect(() => {
    const controlador = new AbortController()

    api.publico
      .filtros(slug, controlador.signal)
      .then(setDisponibles)
      .catch(() => undefined)

    return () => controlador.abort()
  }, [slug])

  // La hoja de filtros es solo del celular. Si la ventana se agranda con la hoja abierta,
  // se cierra: si no, quedaría invisible pero bloqueando el scroll de la página.
  useEffect(() => {
    const escritorio = window.matchMedia('(min-width: 1024px)')
    const cerrar = () => {
      if (escritorio.matches) setHojaAbierta(false)
    }

    escritorio.addEventListener('change', cerrar)
    return () => escritorio.removeEventListener('change', cerrar)
  }, [])

  const cambiar = useCallback(
    (cambios: CambiosDeFiltros) => {
      const siguientes = new URLSearchParams(parametros)

      for (const [clave, valor] of Object.entries(cambios)) {
        if (valor === '') {
          siguientes.delete(clave)
        } else {
          siguientes.set(clave, valor)
        }
      }

      // Cambiar un filtro siempre vuelve a la primera página: quedarse en la cuatro de un
      // resultado que ahora tiene una sola es una pantalla vacía sin explicación.
      siguientes.delete('pagina')

      // Cambiar de marca invalida el modelo elegido, que era de la marca anterior.
      if ('marcaId' in cambios && !('modeloId' in cambios)) siguientes.delete('modeloId')

      setParametros(siguientes, { replace: true })
    },
    [parametros, setParametros],
  )

  const limpiar = useCallback(() => {
    setParametros(new URLSearchParams(), { replace: true })
  }, [setParametros])

  const irAPagina = useCallback(
    (numero: number) => {
      const siguientes = new URLSearchParams(parametros)
      siguientes.set('pagina', String(numero))
      setParametros(siguientes)
      window.scrollTo({ top: 0, behavior: 'smooth' })
    },
    [parametros, setParametros],
  )

  const total = pagina?.total ?? 0
  const panel = <PanelDeFiltros filtros={filtros} disponibles={disponibles} onCambiar={cambiar} />

  return (
    <div className="mx-auto max-w-7xl px-4 pt-8 sm:px-6 lg:pt-12">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="text-3xl font-extrabold tracking-tight text-slate-950 sm:text-4xl">Vehículos</h1>
          <p className="mt-1.5 text-slate-500" aria-live="polite">
            {pagina
              ? `${entero(total)} ${total === 1 ? 'resultado' : 'resultados'}${
                  activos.length > 0 ? ' con tus filtros' : ' disponibles'
                }`
              : 'Buscando…'}
          </p>
        </div>

        <div className="flex w-full items-center gap-2 sm:w-auto">
          <BotonDeFiltros cantidad={activos.length} onClick={() => setHojaAbierta(true)} />
          <Selector
            etiqueta="Ordenar"
            valor={filtros.orden ?? ''}
            vacio="Destacados primero"
            opciones={ORDENES}
            onChange={(valor) => cambiar({ orden: valor })}
            className="flex-1 sm:w-56 sm:flex-none"
          />
        </div>
      </header>

      {activos.length > 0 && <Activos activos={activos} onQuitar={cambiar} onLimpiar={limpiar} />}

      <div className="mt-8 flex gap-10">
        <aside className="hidden w-72 shrink-0 lg:block">
          <div className="sticky top-24 max-h-[calc(100dvh-7rem)] overflow-y-auto rounded-3xl bg-white p-6 shadow-tarjeta ring-1 ring-slate-900/5 [scrollbar-width:thin]">
            {panel}
          </div>
        </aside>

        <section className="min-w-0 flex-1" aria-busy={cargando}>
          {error && <Estado titulo="No pudimos cargar el listado" detalle={error} />}

          {!error && !pagina && (
            <div className="grid gap-5 sm:grid-cols-2 xl:grid-cols-3">
              {[0, 1, 2, 3, 4, 5].map((i) => (
                <Esqueleto key={i} className="h-96 rounded-2xl" />
              ))}
            </div>
          )}

          {!error && pagina && pagina.items.length === 0 && (
            <SinResultados activos={activos} tenant={tenant} onLimpiar={limpiar} />
          )}

          {!error && pagina && pagina.items.length > 0 && (
            <>
              <div
                className={`grid gap-5 transition-opacity duration-300 sm:grid-cols-2 xl:grid-cols-3 ${
                  cargando ? 'opacity-50' : ''
                }`}
              >
                {/* La key es el vehículo: al filtrar entran solo las tarjetas nuevas, y las
                    que siguen en el resultado se quedan quietas. */}
                {pagina.items.map((vehiculo, indice) => (
                  <div
                    key={vehiculo.id}
                    className="h-full motion-safe:animate-entrar"
                    style={{ animationDelay: `${Math.min(indice, 8) * 50}ms` }}
                  >
                    <VehiculoCard vehiculo={vehiculo} base={base} prioridad={indice < 3} />
                  </div>
                ))}
              </div>

              {pagina.totalDePaginas > 1 && (
                <Paginacion actual={pagina.pagina} total={pagina.totalDePaginas} onIr={irAPagina} />
              )}
            </>
          )}
        </section>
      </div>

      <HojaDeFiltros
        abierta={hojaAbierta}
        onCambiarAbierta={setHojaAbierta}
        total={total}
        cargando={cargando}
        hayFiltros={activos.length > 0}
        onLimpiar={limpiar}
      >
        {panel}
      </HojaDeFiltros>

      <FiltrosFlotantes cantidad={activos.length} onClick={() => setHojaAbierta(true)} />
    </div>
  )
}

function BotonDeFiltros({ cantidad, onClick }: { cantidad: number; onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="inline-flex shrink-0 items-center gap-2 rounded-xl bg-white px-4 py-2.5 text-sm font-semibold text-slate-900 ring-1 ring-slate-200 transition hover:ring-slate-400 active:scale-[0.98] lg:hidden"
    >
      <SlidersHorizontal className="size-4" aria-hidden />
      Filtros
      {cantidad > 0 && (
        <span className="grid size-5 place-items-center rounded-full bg-marca text-xs text-marca-contraste">
          {cantidad}
        </span>
      )}
    </button>
  )
}

/**
 * En el celular, los filtros quedan a mano aunque la persona haya bajado: tener que
 * volver arriba de todo para cambiar un filtro es lo que hace abandonar la búsqueda.
 */
function FiltrosFlotantes({ cantidad, onClick }: { cantidad: number; onClick: () => void }) {
  const bajo = useHayScroll(420)

  // Queda montado y se esconde con clases: así puede salir con la misma animación con la
  // que entró, en vez de desaparecer de golpe.
  return (
    <button
      type="button"
      onClick={onClick}
      aria-hidden={!bajo}
      tabIndex={bajo ? 0 : -1}
      className={`fixed bottom-5 left-1/2 z-30 inline-flex -translate-x-1/2 items-center gap-2 rounded-full bg-slate-950 px-5 py-3 text-sm font-semibold text-white shadow-elevada transition-[opacity,translate] duration-300 ease-salida lg:hidden ${
        bajo ? 'translate-y-0 opacity-100' : 'pointer-events-none translate-y-10 opacity-0'
      }`}
    >
      <SlidersHorizontal className="size-4" aria-hidden />
      Filtros
      {cantidad > 0 && (
        <span className="grid size-5 place-items-center rounded-full bg-marca text-xs text-marca-contraste">
          {cantidad}
        </span>
      )}
    </button>
  )
}

function Activos({
  activos,
  onQuitar,
  onLimpiar,
}: {
  activos: FiltroActivo[]
  onQuitar: (cambios: CambiosDeFiltros) => void
  onLimpiar: () => void
}) {
  return (
    <div className="mt-5 flex flex-wrap items-center gap-2">
      {activos.map((activo) => (
        <button
          key={activo.clave}
          type="button"
          onClick={() => onQuitar(activo.quitar)}
          aria-label={`Quitar ${activo.texto}`}
          className="inline-flex items-center gap-1.5 rounded-full bg-marca/10 py-1.5 pl-3 pr-2 text-sm font-medium text-marca-tinta transition-colors hover:bg-marca/20 motion-safe:animate-aparecer"
        >
          {activo.texto}
          <X className="size-3.5" aria-hidden />
        </button>
      ))}

      <button
        type="button"
        onClick={onLimpiar}
        className="px-2 text-sm font-medium text-slate-500 underline-offset-4 transition hover:text-slate-900 hover:underline"
      >
        Limpiar todo
      </button>
    </div>
  )
}

/**
 * Sin resultados, el listado no se queda en "no hay": ofrece contarle a la automotora lo
 * que se buscaba, con los filtros ya escritos en el mensaje.
 */
function SinResultados({
  activos,
  tenant,
  onLimpiar,
}: {
  activos: FiltroActivo[]
  tenant: TenantPublico
  onLimpiar: () => void
}) {
  const buscado = activos.map((activo) => activo.texto).join(', ')

  return (
    <div className="flex flex-col items-center rounded-3xl bg-slate-50 px-6 py-16 text-center ring-1 ring-slate-900/5">
      <span className="grid size-14 place-items-center rounded-2xl bg-white text-slate-500 shadow-sm ring-1 ring-slate-900/5">
        <SearchX className="size-7" aria-hidden />
      </span>
      <h2 className="mt-5 text-xl font-bold text-slate-900">No encontramos vehículos con esos filtros</h2>
      <p className="mt-2 max-w-md text-slate-600">
        Probá ampliando el rango de precio o de año. Anotamos lo que buscaste: nos sirve para
        saber qué traer.
      </p>

      <div className="mt-6 flex flex-wrap justify-center gap-3">
        <button
          type="button"
          onClick={onLimpiar}
          className="rounded-full bg-white px-5 py-2.5 text-sm font-semibold text-slate-900 ring-1 ring-slate-200 transition hover:ring-slate-400"
        >
          Limpiar filtros
        </button>

        {tenant.whatsapp && (
          <a
            href={linkDeWhatsapp(
              tenant.whatsapp,
              buscado ? `Hola, estoy buscando: ${buscado}. ¿Les entra algo así?` : 'Hola, estoy buscando un auto.',
            )}
            target="_blank"
            rel="noreferrer"
            className="inline-flex items-center gap-2 rounded-full bg-emerald-600 px-5 py-2.5 text-sm font-semibold text-white transition hover:bg-emerald-700"
          >
            <IconoWhatsapp className="size-4" />
            Avisame cuando entre
          </a>
        )}
      </div>
    </div>
  )
}

function Paginacion({ actual, total, onIr }: { actual: number; total: number; onIr: (pagina: number) => void }) {
  const boton =
    'grid size-10 place-items-center rounded-full text-sm font-semibold transition disabled:pointer-events-none disabled:opacity-30'

  return (
    <nav aria-label="Páginas" className="mt-12 flex items-center justify-center gap-1.5">
      <button
        type="button"
        aria-label="Página anterior"
        disabled={actual <= 1}
        onClick={() => onIr(actual - 1)}
        className={`${boton} text-slate-700 hover:bg-slate-100`}
      >
        <ChevronLeft className="size-5" aria-hidden />
      </button>

      {paginasVisibles(actual, total).map((numero, indice) =>
        numero === null ? (
          <span key={`salto-${indice}`} className="px-1 text-slate-400" aria-hidden>
            …
          </span>
        ) : (
          <button
            key={numero}
            type="button"
            aria-current={numero === actual ? 'page' : undefined}
            onClick={() => onIr(numero)}
            className={`${boton} ${
              numero === actual ? 'bg-marca text-marca-contraste' : 'text-slate-700 hover:bg-slate-100'
            }`}
          >
            {numero}
          </button>
        ),
      )}

      <button
        type="button"
        aria-label="Página siguiente"
        disabled={actual >= total}
        onClick={() => onIr(actual + 1)}
        className={`${boton} text-slate-700 hover:bg-slate-100`}
      >
        <ChevronRight className="size-5" aria-hidden />
      </button>
    </nav>
  )
}

/** La primera, la última y las vecinas de la actual; `null` marca un salto. */
function paginasVisibles(actual: number, total: number): (number | null)[] {
  const elegidas = [...new Set([1, actual - 1, actual, actual + 1, total])]
    .filter((numero) => numero >= 1 && numero <= total)
    .sort((a, b) => a - b)

  return elegidas.flatMap((numero, indice) =>
    indice > 0 && numero - elegidas[indice - 1] > 1 ? [null, numero] : [numero],
  )
}

/** Los filtros en una hoja que sube desde abajo, como en las apps del celular. */
function HojaDeFiltros({
  abierta,
  onCambiarAbierta,
  total,
  cargando,
  hayFiltros,
  onLimpiar,
  children,
}: {
  abierta: boolean
  onCambiarAbierta: (abierta: boolean) => void
  total: number
  cargando: boolean
  hayFiltros: boolean
  onLimpiar: () => void
  children: ReactNode
}) {
  return (
    <Dialog.Root open={abierta} onOpenChange={onCambiarAbierta}>
      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 z-50 bg-slate-950/40 backdrop-blur-sm data-[state=closed]:animate-desaparecer data-[state=open]:animate-aparecer lg:hidden" />
        <Dialog.Content
          aria-describedby={undefined}
          className="fixed inset-x-0 bottom-0 z-50 flex max-h-[88dvh] flex-col rounded-t-3xl bg-white shadow-elevada outline-none data-[state=closed]:animate-bajar data-[state=open]:animate-subir lg:hidden"
        >
          <div aria-hidden className="mx-auto mt-3 h-1.5 w-12 shrink-0 rounded-full bg-slate-200" />

          <div className="flex items-center justify-between px-5 pb-2 pt-3">
            <Dialog.Title className="text-lg font-bold text-slate-950">Filtros</Dialog.Title>
            <Dialog.Close
              aria-label="Cerrar"
              className="grid size-9 place-items-center rounded-full text-slate-500 transition hover:bg-slate-100 hover:text-slate-900"
            >
              <X className="size-5" aria-hidden />
            </Dialog.Close>
          </div>

          <div className="flex-1 overflow-y-auto overscroll-contain px-5 pb-6 pt-2">{children}</div>

          {/* El total se actualiza mientras se filtra: el botón dice cuánto va a ver la
              persona antes de cerrar la hoja. */}
          <div className="flex gap-3 border-t border-slate-100 px-5 pt-4 pb-[max(1rem,env(safe-area-inset-bottom))]">
            {hayFiltros && (
              <button
                type="button"
                onClick={onLimpiar}
                className="rounded-xl px-4 py-3 text-sm font-semibold text-slate-600 ring-1 ring-slate-200"
              >
                Limpiar
              </button>
            )}
            <Dialog.Close className="flex-1 rounded-xl bg-marca py-3 text-sm font-semibold text-marca-contraste transition active:scale-[0.98]">
              {cargando ? 'Buscando…' : `Ver ${entero(total)} ${total === 1 ? 'vehículo' : 'vehículos'}`}
            </Dialog.Close>
          </div>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  )
}
