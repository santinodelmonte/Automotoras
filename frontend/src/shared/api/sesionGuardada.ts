import type { Sesion } from './types'

const CLAVE = 'automotora.sesion'

/**
 * La sesión sobrevive al refresh de la página, guardada en `localStorage`.
 *
 * Es una decisión con contrapartida: `localStorage` es legible desde JavaScript, así que
 * un XSS se lleva los tokens. La alternativa —cookie `HttpOnly`— los protege de eso pero
 * obliga a manejar CSRF y a que la API y el sitio compartan dominio, y con dominios
 * propios por automotora eso deja de ser cierto. Mientras el access token dure minutos y
 * el refresh sea revocable y rote en cada uso, la ventana de daño es acotada.
 */
export const sesionGuardada = {
  leer(): Sesion | null {
    const crudo = localStorage.getItem(CLAVE)
    if (!crudo) return null

    try {
      return JSON.parse(crudo) as Sesion
    } catch {
      // Un valor corrupto no puede dejar la aplicación sin poder arrancar.
      localStorage.removeItem(CLAVE)
      return null
    }
  },

  guardar(sesion: Sesion) {
    localStorage.setItem(CLAVE, JSON.stringify(sesion))
  },

  borrar() {
    localStorage.removeItem(CLAVE)
  },
}
