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
  return `${simbolos[moneda] ?? moneda} ${numero.format(monto)}`
}

export function kilometros(km: number): string {
  return `${numero.format(km)} km`
}

export function entero(valor: number): string {
  return numero.format(valor)
}

export function fecha(iso: string | null): string {
  if (!iso) return '—'

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
