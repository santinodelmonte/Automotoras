# `src/public`

Sitio público de cada tenant. Sin autenticación. El tenant lo resuelve el servidor por
dominio propio o por el slug de la ruta (`/t/{slug}` en desarrollo), y siempre contra la
tabla de automotoras.

- `SitioPublicoLayout.tsx` — resuelve la automotora y pinta cabecera, pie y branding.
- `TenantContexto.ts` — la automotora del sitio, para el resto de las pantallas.
- `VehiculoCard.tsx` — la tarjeta del listado y de la home.
- `paginas/` — home con destacados, listado con los filtros del brief y ficha con galería,
  ficha técnica y botones de contacto.

Las mismas pantallas se montan dos veces en el router: en la raíz, que es como entra cada
automotora por su dominio, y bajo `/t/{slug}`, que es como se trabaja en desarrollo.
