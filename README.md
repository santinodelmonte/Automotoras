# Automotora SaaS

SaaS multi-tenant para automotoras: cada cliente tiene su propio sitio público de venta de
vehículos —con su dominio, su logo y sus colores— y un panel de administración privado
para gestionar el stock. Una sola aplicación y una sola base de datos atienden a todos los
tenants; la identidad de cada automotora se resuelve por configuración, no por deploy
separado.

El diferencial no es el catálogo, que es commodity, sino la inteligencia de demanda: el
sistema mide qué vehículos miran los compradores, cuáles consultan, qué buscan y no
encuentran, y cuánto tiempo queda cada unidad en góndola. Con eso el dueño de la
automotora decide qué stock comprar con datos en vez de intuición. Por eso los datos viven
juntos y por eso el tracking de eventos se instrumenta desde el primer día.

El detalle completo de alcance, modelo de datos y reglas de multi-tenancy está en
[docs/brief.md](docs/brief.md).

> **Estado actual: Paso 0 — esqueleto ejecutable.** El repositorio compila, corre, tiene
> tests pasando y el frontend consume la API. Todavía no hay entidades, base de datos,
> autenticación ni features.

## Requisitos previos

| Herramienta | Versión | Notas |
| --- | --- | --- |
| .NET SDK | 8.0.x | La versión está fijada en [`global.json`](global.json). Si tenés instalado el SDK 10, igual se usa el 8. |
| Node.js | 20.19+ / 22.12+ | Probado con Node 24. |
| npm | 10+ | |
| MySQL | 8.0 | Todavía no hace falta: no hay base de datos en el Paso 0. |

## Cómo levantarlo

### Backend

```bash
cd backend/Api && dotnet run
```

Queda escuchando en `http://localhost:5080`.

- Health check: <http://localhost:5080/api/health>
- Swagger (solo en Development): <http://localhost:5080/swagger>

### Frontend

```bash
cd frontend && npm install && npm run dev
```

Queda escuchando en <http://localhost:5173> y muestra en pantalla la respuesta de
`GET /api/health`. Levantá el backend primero o la pantalla va a mostrar el error de
conexión.

### Tests

```bash
dotnet test
```

## Variables de entorno

### Backend

`backend/Api/appsettings.json` está versionado con las claves vacías. Los valores reales se
proveen por variables de entorno o por un `appsettings.Development.json` local (ignorado por
git). La forma esperada, con explicación clave por clave, está documentada en
[`backend/Api/appsettings.Example.json`](backend/Api/appsettings.Example.json).

En variables de entorno, el anidamiento se expresa con doble guion bajo
(`ConnectionStrings__Default`, `Jwt__Secret`, …).

| Clave | Variable de entorno | Para qué |
| --- | --- | --- |
| `ConnectionStrings:Default` | `ConnectionStrings__Default` | Conexión a MySQL. |
| `Jwt:Issuer` / `Jwt:Audience` | `Jwt__Issuer` / `Jwt__Audience` | Emisor y audiencia de los tokens. |
| `Jwt:Secret` | `Jwt__Secret` | Clave de firma. Mínimo 32 caracteres, aleatoria. Nunca versionar. |
| `Jwt:AccessTokenMinutes` | `Jwt__AccessTokenMinutes` | Vida del access token. |
| `Jwt:RefreshTokenDays` | `Jwt__RefreshTokenDays` | Vida del refresh token. |
| `Storage:Provider` | `Storage__Provider` | `Local` en desarrollo, `R2` en producción. |
| `Storage:LocalRootPath` | `Storage__LocalRootPath` | Solo con `Provider=Local`: carpeta de subidas, fuera del repo. |
| `Storage:PublicBaseUrl` | `Storage__PublicBaseUrl` | URL pública desde la que se sirven las imágenes. |
| `Storage:Bucket` / `Storage:Endpoint` | `Storage__Bucket` / `Storage__Endpoint` | Bucket y endpoint S3-compatible (Cloudflare R2). |
| `Storage:AccessKeyId` / `Storage:SecretAccessKey` | `Storage__AccessKeyId` / `Storage__SecretAccessKey` | Credenciales del object storage. Nunca versionar. |
| `Jobs:Secret` | `Jobs__Secret` | Valor esperado en el header `X-Job-Secret` de `POST /api/jobs/{nombre}`. |
| `Cors:AllowedOrigins` | `Cors__AllowedOrigins__0` | Orígenes del frontend habilitados. En desarrollo, `http://localhost:5173`. |

### Frontend

Copiá [`frontend/.env.example`](frontend/.env.example) a `frontend/.env`:

| Variable | Para qué |
| --- | --- |
| `VITE_API_BASE_URL` | Base URL de la API. En desarrollo, `http://localhost:5080`. |

## Estructura

```
/backend
  /Api                 controllers, middleware, DI, Program.cs
  /Core                entidades, interfaces, DTOs, lógica de dominio — no referencia a nadie
  /Infrastructure      DbContext, repositorios, migraciones EF, storage, servicios externos
  /Tests               xUnit
/frontend
  /src
    /admin             panel privado
    /public            sitio público del tenant
    /shared            componentes, hooks, cliente de API, tipos
/docs                  brief del proyecto
```

Referencias entre proyectos: `Api → Core, Infrastructure`; `Infrastructure → Core`;
`Tests → Core, Infrastructure, Api` (la referencia a `Api` es la que permite levantar la
API en memoria con `WebApplicationFactory` en los tests de integración). `Core` no
referencia a nadie.

## Restricciones de deploy

El destino inicial es SmarterASP.NET (shared hosting Windows/IIS). El código tiene que
respetar tres reglas desde el principio:

1. **Nunca escribir archivos en el disco del servidor.** Todo binario va a object storage
   detrás de la interfaz `IImageStorage`.
2. **Nada de `IHostedService` / `BackgroundService` para trabajo crítico.** El app pool de
   IIS recicla de forma impredecible. Los jobs periódicos se exponen como
   `POST /api/jobs/{nombre}`, protegidos por el header `X-Job-Secret` y disparados por un
   cron externo.
3. **Nada hardcodeado.** Toda configuración sale de variables de entorno o de `appsettings`
   sobrescribible.
