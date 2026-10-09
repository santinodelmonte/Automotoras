import { useLayoutEffect, useRef, useState } from 'react'
import { simboloDeMoneda } from '@shared/ui/formato'

interface Props {
  valor: number | null
  onCambio: (valor: number | null) => void
  /** El símbolo va adentro del campo, a la izquierda: "US$" o "$". */
  moneda?: string
  /** Una unidad adentro del campo, a la derecha: "km". */
  sufijo?: string
  /** Sin decimales la coma no se acepta: un kilometraje es un entero. */
  decimales?: boolean
  required?: boolean
  disabled?: boolean
  id?: string
}

/**
 * Un número escrito como se lee en Uruguay: punto para los miles y coma para los
 * decimales. "332000" se ve "332.000" mientras se escribe.
 *
 * No es un `type="number"` porque ese input no deja mostrar separadores. Es texto con
 * `inputMode`, que en el celular sigue abriendo el teclado numérico. Afuera sale siempre
 * un número —o `null` si está vacío—; el texto con puntos queda adentro.
 *
 * El punto lo pone el componente: escrito a mano no cuenta, así que "13600.50" no es un
 * decimal sino 1.360.050. Se nota al instante porque el punto desaparece al tipearlo.
 */
export function EntradaNumerica({
  valor,
  onCambio,
  moneda,
  sufijo,
  decimales = true,
  required,
  disabled,
  id,
}: Props) {
  const entrada = useRef<HTMLInputElement>(null)
  const [texto, setTexto] = useState(() => formatear(valor, decimales))
  // Dónde dejar el cursor después de repintar, contado en dígitos y no en caracteres:
  // al agregarse un punto de miles, la posición en caracteres se corre.
  const cursor = useRef<number | null>(null)

  // Si el valor cambia desde afuera (se cargó el vehículo), el texto lo sigue. Mientras
  // se escribe no: "1.000," todavía vale 1000 y no hay que pisarle la coma. Vacío y cero
  // también conviven: quien guarda 0 al borrar no tiene que ver aparecer un "0" que
  // después se le pega adelante de lo que escribe.
  if (interpretar(texto) !== valor && !(texto === '' && valor === 0)) {
    setTexto(formatear(valor, decimales))
  }

  useLayoutEffect(() => {
    if (cursor.current === null || !entrada.current) return

    const posicion = posicionTrasDigitos(texto, cursor.current)
    entrada.current.setSelectionRange(posicion, posicion)
    cursor.current = null
  }, [texto])

  function cambiar(evento: React.ChangeEvent<HTMLInputElement>) {
    const crudo = evento.target.value
    const hastaElCursor = crudo.slice(0, evento.target.selectionStart ?? crudo.length)

    const nuevo = normalizar(crudo, decimales)
    cursor.current = contarDigitos(hastaElCursor)
    setTexto(nuevo)
    onCambio(interpretar(nuevo))
  }

  const simbolo = moneda ? simboloDeMoneda(moneda) : null

  return (
    <div className="relative">
      {simbolo && (
        <span aria-hidden className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-sm text-slate-400">
          {simbolo}
        </span>
      )}
      <input
        ref={entrada}
        id={id}
        type="text"
        inputMode={decimales ? 'decimal' : 'numeric'}
        autoComplete="off"
        required={required}
        disabled={disabled}
        value={texto}
        onChange={cambiar}
        className={`panel-entrada tabular-nums ${simbolo ? (simbolo.length > 1 ? 'pl-12' : 'pl-7') : ''} ${sufijo ? 'pr-11' : ''}`}
      />
      {sufijo && (
        <span aria-hidden className="pointer-events-none absolute right-3 top-1/2 -translate-y-1/2 text-sm text-slate-400">
          {sufijo}
        </span>
      )}
    </div>
  )
}

/** Deja solo dígitos —y una coma con hasta dos decimales, si se aceptan— y agrupa los miles con punto. */
function normalizar(crudo: string, decimales: boolean): string {
  // La coma cuenta, y solo la primera.
  const limpio = crudo.replace(decimales ? /[^\d,]/g : /\D/g, '')
  const coma = limpio.indexOf(',')
  const entera = (coma === -1 ? limpio : limpio.slice(0, coma)).replace(/^0+(?=\d)/, '')
  const parteDecimal = coma === -1 ? null : limpio.slice(coma + 1).replace(/,/g, '').slice(0, 2)

  const agrupada = entera.replace(/\B(?=(\d{3})+(?!\d))/g, '.')

  if (parteDecimal === null) return agrupada
  return `${agrupada || '0'},${parteDecimal}`
}

function formatear(valor: number | null, decimales: boolean): string {
  if (valor === null) return ''

  const [entera, parteDecimal] = String(valor).split('.')
  return normalizar(parteDecimal ? `${entera},${parteDecimal}` : entera, decimales)
}

function interpretar(texto: string): number | null {
  if (texto === '') return null

  const numero = Number(texto.replace(/\./g, '').replace(',', '.'))
  return Number.isFinite(numero) ? numero : null
}

function contarDigitos(texto: string): number {
  return texto.replace(/[^\d,]/g, '').length
}

function posicionTrasDigitos(texto: string, digitos: number): number {
  if (digitos === 0) return 0

  let vistos = 0
  for (let i = 0; i < texto.length; i++) {
    if (/[\d,]/.test(texto[i])) vistos++
    if (vistos === digitos) return i + 1
  }

  return texto.length
}
