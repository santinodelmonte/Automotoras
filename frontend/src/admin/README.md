# `src/admin`

Panel privado de la automotora (y del SuperAdmin). Todo lo que vive detrás del login
con JWT.

- `AdminLayout.tsx` — navegación, recortada por rol.
- `InicioDelPanel.tsx` — adónde cae cada rol al entrar. El tablero es del dueño; el
  vendedor arranca en el stock; el SuperAdmin, en la lista de automotoras.
- `LoginPage.tsx` — login del panel.
- `imagenes.ts` — achica las fotos en el navegador antes de subirlas. Es lo que hace
  cumplible el criterio de cargar diez fotos desde el celular sin timeout, y de paso saca
  los metadatos EXIF, incluida la geolocalización.
- `GaleriaDeFotos.tsx` — subir, reordenar, elegir portada y borrar.
- `CambiarEstado.tsx` — cambio rápido de estado. Marcar vendido pide fecha y precio.
- `paginas/` — tablero, stock, alta y edición de vehículos, usuarios, configuración, y
  las tres pantallas de SuperAdmin.
