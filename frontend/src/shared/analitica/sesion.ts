const CLAVE = 'automotora.visita'

/**
 * Identificador de la visita, para contar personas distintas sin saber quién es nadie.
 *
 * Vive en `sessionStorage`: dura lo que la pestaña abierta y muere al cerrarla. Alcanza
 * para que cuatro búsquedas de la misma persona cuenten como una visita y no como cuatro,
 * que es para lo único que se usa, y no permite seguir a nadie de un día para otro. Antes
 * estaba en `localStorage` y duraba para siempre: era un identificador persistente del
 * visitante, un dato personal que ningún reporte necesitaba.
 *
 * Si el navegador no deja guardar nada (modo privado estricto), se manda sin
 * identificador: la visita no se cuenta como distinta, que es lo correcto antes que
 * inventarla.
 */
export function idDeVisita(): string | null {
  try {
    // El identificador viejo, persistente, se borra en cuanto se ve.
    localStorage.removeItem(CLAVE)

    const guardado = sessionStorage.getItem(CLAVE)
    if (guardado) return guardado

    const nuevo = crypto.randomUUID()
    sessionStorage.setItem(CLAVE, nuevo)

    return nuevo
  } catch {
    return null
  }
}
