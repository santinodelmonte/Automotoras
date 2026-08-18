# `src/admin`

Panel privado de la automotora (y del SuperAdmin). Todo lo que vive detrás del login
con JWT: CRUD de vehículos, gestión de usuarios, configuración del tenant, dashboard.

- `LoginPage.tsx` — login del panel. Al entrar, la sesión queda en el store del cliente
  de API y todas las llamadas posteriores viajan con el token.
- `PanelPage.tsx` — panel de la sesión abierta. Muestra el usuario y, si es Owner, los
  usuarios de la automotora.

El ABM de vehículos, la carga de fotos y el dashboard llegan con las features de fase 1.
