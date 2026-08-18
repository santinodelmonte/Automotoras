# `src/shared`

Código compartido entre el panel privado y el sitio público: cliente de API tipado,
tipos del dominio, componentes y hooks reutilizables.

- `api/client.ts` — cliente HTTP tipado. La base URL sale de `VITE_API_BASE_URL`. Agrega
  el `Authorization: Bearer`, y ante un 401 con sesión abierta renueva el token una vez y
  reintenta. Los refrescos se serializan en uno solo: como el refresh token rota en cada
  uso, varios en paralelo llegarían con un token ya quemado y el servidor lo leería como
  reuso, cerrando todas las sesiones del usuario.
- `api/sesionGuardada.ts` — persistencia de la sesión entre recargas.
- `api/types.ts` — tipos de las respuestas de la API.
- `analitica/sesion.ts` — id de la visita, para agrupar la actividad de una persona sin
  saber quién es. Lo genera el cliente y no una cookie del servidor: el sitio y la API
  viven en orígenes distintos, así que una cookie del servidor sería de tercera parte y
  los navegadores la bloquean.
- `ui/formato.ts` — precios, kilómetros y fechas en formato uruguayo.
- `ui/Estado.tsx` — pantallas de carga, vacío y error.
- `auth/useSesion.ts` — la sesión en curso, suscripta al store del cliente, para que una
  renovación en segundo plano repinte la UI sola.
- `auth/RutaProtegida.tsx` — corta el paso por rol en el cliente. No es la seguridad: la
  seguridad está en el servidor, que responde 401 y 403 aunque el navegador pinte lo que
  sea.
