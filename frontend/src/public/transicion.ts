import type { MouseEvent } from 'react'
import type { VehiculoPublicoResumen } from '@shared/api/types'

/**
 * Lo que una tarjeta le pasa a la ficha al navegar: los datos que ya tiene. Con eso la
 * ficha muestra foto, título y precio en el acto mientras pide el resto, en vez de una
 * pantalla vacía.
 */
export interface EstadoDeFicha {
  previa?: VehiculoPublicoResumen
}

/** El nombre que une la foto de la tarjeta con la foto principal de la ficha. */
const NOMBRE = 'foto-vehiculo'

let marcada: HTMLElement | null = null

/**
 * Le da a una foto el nombre de la transición, y se lo saca a la que lo tenía.
 *
 * El nombre no puede estar fijo en todas las tarjetas: en cada página tiene que haber una
 * sola foto con ese nombre, o el navegador cancela la transición. Por eso la tarjeta lo
 * recibe recién en el clic, y la ficha al pintar su foto principal.
 */
export function marcarFoto(foto: HTMLElement | null) {
  if (marcada && marcada !== foto) marcada.style.viewTransitionName = ''
  if (foto) foto.style.viewTransitionName = NOMBRE
  marcada = foto
}

/**
 * Marca la foto de una tarjeta antes de navegar a su ficha. Un clic con Ctrl o con la
 * rueda abre otra pestaña: esta página no navega y la foto no tiene que viajar.
 */
export function alTocarTarjeta(evento: MouseEvent, foto: HTMLElement | null) {
  if (evento.button !== 0 || evento.metaKey || evento.ctrlKey || evento.shiftKey || evento.altKey) {
    return
  }

  marcarFoto(foto)
}
