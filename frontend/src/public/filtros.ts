import { entero, etiqueta, kilometros, simboloDeMoneda } from '@shared/ui/formato'
import type { FiltrosDisponibles, FiltrosPublicos } from '@shared/api/types'

/** Un cambio de filtros: clave del query string y valor nuevo, vacío para sacarla. */
export type CambiosDeFiltros = Record<string, string>

/**
 * Los filtros viven en el query string, no en el estado del componente.
 *
 * Es lo que hace que un listado filtrado se pueda compartir por WhatsApp, que el botón
 * "atrás" del navegador vuelva a los filtros anteriores y que recargar no borre lo que el
 * comprador venía armando.
 */
export function leerFiltros(parametros: URLSearchParams): FiltrosPublicos {
  const numero = (clave: string) => {
    const valor = parametros.get(clave)
    return valor ? Number(valor) : undefined
  }

  const texto = (clave: string) => parametros.get(clave) ?? undefined

  return {
    marcaId: numero('marcaId'),
    modeloId: numero('modeloId'),
    anioDesde: numero('anioDesde'),
    anioHasta: numero('anioHasta'),
    moneda: texto('moneda'),
    precioDesde: numero('precioDesde'),
    precioHasta: numero('precioHasta'),
    kmDesde: numero('kmDesde'),
    kmHasta: numero('kmHasta'),
    combustible: texto('combustible'),
    transmision: texto('transmision'),
    carroceria: texto('carroceria'),
    orden: texto('orden'),
    pagina: numero('pagina') ?? 1,
  }
}

export interface FiltroActivo {
  clave: string
  texto: string
  /** Lo que hay que sacar del query string para quitarlo. */
  quitar: CambiosDeFiltros
}

/**
 * Los filtros aplicados, en palabras. Son los chips que se quitan con un toque y también
 * el mensaje de WhatsApp cuando no hay resultados: "estoy buscando Toyota, Hilux, hasta
 * 60.000 km" le dice a la automotora exactamente qué le están pidiendo.
 */
export function describirFiltros(
  filtros: FiltrosPublicos,
  disponibles: FiltrosDisponibles | null,
): FiltroActivo[] {
  const activos: FiltroActivo[] = []
  const marca = disponibles?.marcas.find((m) => m.id === filtros.marcaId)

  if (filtros.marcaId) {
    activos.push({ clave: 'marca', texto: marca?.nombre ?? 'Marca', quitar: { marcaId: '', modeloId: '' } })
  }

  if (filtros.modeloId) {
    const modelo = marca?.modelos.find((m) => m.id === filtros.modeloId)
    activos.push({ clave: 'modelo', texto: modelo?.nombre ?? 'Modelo', quitar: { modeloId: '' } })
  }

  const anios = rango(filtros.anioDesde, filtros.anioHasta, String)
  if (anios) activos.push({ clave: 'anio', texto: anios, quitar: { anioDesde: '', anioHasta: '' } })

  // El precio no existe sin su moneda: quitar uno quita los dos, como pide la API.
  if (filtros.moneda) {
    const simbolo = simboloDeMoneda(filtros.moneda)
    const precios = rango(filtros.precioDesde, filtros.precioHasta, (valor) => `${simbolo} ${entero(valor)}`)

    activos.push({
      clave: 'precio',
      texto: precios ?? `En ${etiqueta(filtros.moneda).toLowerCase()}`,
      quitar: { moneda: '', precioDesde: '', precioHasta: '' },
    })
  }

  const km = rango(filtros.kmDesde, filtros.kmHasta, kilometros)
  if (km) activos.push({ clave: 'km', texto: km, quitar: { kmDesde: '', kmHasta: '' } })

  if (filtros.carroceria) {
    activos.push({ clave: 'carroceria', texto: etiqueta(filtros.carroceria), quitar: { carroceria: '' } })
  }

  if (filtros.combustible) {
    activos.push({ clave: 'combustible', texto: etiqueta(filtros.combustible), quitar: { combustible: '' } })
  }

  if (filtros.transmision) {
    activos.push({ clave: 'transmision', texto: etiqueta(filtros.transmision), quitar: { transmision: '' } })
  }

  return activos
}

/** "2015 – 2020", "Desde 2015" o "Hasta 2020". */
function rango(
  desde: number | undefined,
  hasta: number | undefined,
  formatear: (valor: number) => string,
): string | null {
  if (desde !== undefined && hasta !== undefined) return `${formatear(desde)} – ${formatear(hasta)}`
  if (desde !== undefined) return `Desde ${formatear(desde)}`
  if (hasta !== undefined) return `Hasta ${formatear(hasta)}`
  return null
}
