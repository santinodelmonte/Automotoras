const CLAVE = 'automotora.visita'

/**
 * Identificador de la visita, para poder agrupar la actividad de una misma persona sin
 * saber quién es.
 *
 * Lo genera y lo guarda el cliente, no una cookie del servidor. El brief pedía cookie de
 * primera parte, pero el sitio y la API viven en orígenes distintos —y con dominio propio
 * por automotora eso no cambia—, así que una cookie del servidor sería de tercera parte:
 * los navegadores la bloquean por defecto y el dato se perdería justo donde más tráfico
 * hay. Esto cumple lo mismo y no depende de la política de cookies de nadie.
 */
export function idDeVisita(): string {
  const guardado = localStorage.getItem(CLAVE)
  if (guardado) return guardado

  const nuevo = crypto.randomUUID()
  localStorage.setItem(CLAVE, nuevo)

  return nuevo
}
