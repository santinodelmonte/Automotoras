/**
 * Formatos de presentación. Uruguay usa `es-UY`: punto para los miles y coma para los
 * decimales.
 */
const numero = new Intl.NumberFormat('es-UY', { maximumFractionDigits: 0 })

const fechaCorta = new Intl.DateTimeFormat('es-UY', {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
})

/** Símbolos de las monedas del enum de la API. */
const simbolos: Record<string, string> = {
  Usd: 'US$',
  Uyu: '$',
}

export function precio(monto: number, moneda: string): string {
  return `${simboloDeMoneda(moneda)} ${numero.format(monto)}`
}

export function simboloDeMoneda(moneda: string): string {
  return simbolos[moneda] ?? moneda
}

/**
 * Cómo se lee cada valor de los enums de la API. Viajan como identificadores
 * (`Automatica`, `Suv`), que sirven para filtrar pero no para mostrárselos a un comprador.
 * Lo que no está acá ya se lee bien tal cual.
 */
const etiquetas: Record<string, string> = {
  Sedan: 'Sedán',
  Suv: 'SUV',
  Coupe: 'Coupé',
  Wagon: 'Rural',
  Diesel: 'Diésel',
  Hibrido: 'Híbrido',
  Electrico: 'Eléctrico',
  Gnc: 'GNC',
  Automatica: 'Automática',
  Usd: 'Dólares',
  Uyu: 'Pesos',
}

export function etiqueta(valor: string): string {
  return etiquetas[valor] ?? valor
}

export function kilometros(km: number): string {
  return `${numero.format(km)} km`
}

export function entero(valor: number): string {
  return numero.format(valor)
}

export function fecha(iso: string | null): string {
  if (!iso) return '—'

  // Una fecha sin hora (`2026-12-07`, como viajan los vencimientos) se arma en la zona
  // local. `new Date('2026-12-07')` la toma como medianoche UTC, y en Uruguay se vería
  // como el día anterior.
  const soloDia = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso)
  if (soloDia) {
    return fechaCorta.format(new Date(Number(soloDia[1]), Number(soloDia[2]) - 1, Number(soloDia[3])))
  }

  const valor = new Date(iso)
  return Number.isNaN(valor.getTime()) ? '—' : fechaCorta.format(valor)
}

/** `2026-08-18`, que es lo que espera un `<input type="date">`. */
export function paraInputDate(iso: string | null): string {
  if (!iso) return ''

  const valor = new Date(iso)
  return Number.isNaN(valor.getTime()) ? '' : valor.toISOString().slice(0, 10)
}

/**
 * Link de WhatsApp con el mensaje ya puesto. El número va sin símbolos porque es lo único
 * que acepta `wa.me`.
 */
export function linkDeWhatsapp(numeroDeTelefono: string, mensaje: string): string {
  return `https://wa.me/${numeroDeTelefono.replace(/\D/g, '')}?text=${encodeURIComponent(mensaje)}`
}

/** Link de Google Maps que busca la dirección. No necesita clave ni coordenadas. */
export function linkDeMapa(direccion: string): string {
  return `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(direccion)}`
}
