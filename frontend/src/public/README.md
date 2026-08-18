# `src/public`

Sitio público de cada tenant: home con destacados, listado con filtros y ficha de
vehículo. Sin autenticación. El tenant se resuelve por dominio custom o por el slug
de la ruta (`/t/{slug}` en desarrollo).

- `SitioPublico.tsx` — resuelve la automotora contra la API y se pinta con su identidad.
  El tenant no se elige acá: lo resuelve el servidor y, si no matchea, responde 404.

El catálogo, los filtros y las fichas llegan con las features de fase 1.
