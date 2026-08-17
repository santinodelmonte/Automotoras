# `src/shared`

Código compartido entre el panel privado y el sitio público: cliente de API tipado,
tipos del dominio, componentes y hooks reutilizables.

- `api/client.ts` — cliente HTTP tipado. La base URL sale de `VITE_API_BASE_URL`.
  Acá van a vivir el interceptor que agrega el JWT y el manejo de refresh en 401.
- `api/types.ts` — tipos de las respuestas de la API.
