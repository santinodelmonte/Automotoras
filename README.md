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

> **Estado actual: paso 2 — modelo de datos.** El esqueleto corre, el frontend consume la
> API, y están las entidades, el `DbContext` con los filtros globales por tenant y la
> migración inicial. Todavía no hay autenticación, resolución de tenant ni features:
> el `ITenantContext` existe pero nadie lo resuelve aún, así que por diseño no se lee ni
> se escribe nada.

## Requisitos previos

| Herramienta | Versión | Notas |
| --- | --- | --- |
| .NET SDK | 8.0.x | La versión está fijada en [`global.json`](global.json). Si tenés instalado el SDK 10, igual se usa el 8. |
| Node.js | 20.19+ / 22.12+ | Probado con Node 24. |
| npm | 10+ | |
| MySQL | 8.0 | Necesario para aplicar migraciones y correr contra una base real. La API levanta y `/api/health` responde sin él. |

Las herramientas de EF Core están fijadas en el repo ([`.config/dotnet-tools.json`](.config/dotnet-tools.json)).
Después de clonar:

```bash
dotnet tool restore
```

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

Los tests de persistencia corren sobre SQLite en memoria, así que no hace falta MySQL para
ejecutarlos. Se eligió SQLite y no el proveedor `InMemory` a propósito: el `InMemory` no
traduce a SQL y evalúa los filtros con semántica de C#, con lo cual un filtro de tenant
roto podría pasar el test igual.

### Migraciones

```bash
dotnet dotnet-ef migrations add NombreDeLaMigracion --project backend/Infrastructure --startup-project backend/Infrastructure --output-dir Persistence/Migrations
```

Generar una migración no necesita una base viva: se resuelve con la factory de diseño y la
versión de MySQL declarada. Para aplicarla sí hace falta MySQL:

```bash
ConnectionStrings__Default="Server=localhost;Port=3306;Database=automotora_saas;User Id=root;Password=...;" dotnet dotnet-ef database update --project backend/Infrastructure --startup-project backend/Infrastructure
```

En SmarterASP.NET, donde no hay CLI, conviene generar el SQL y aplicarlo desde el panel:

```bash
dotnet dotnet-ef migrations script --idempotent --project backend/Infrastructure --startup-project backend/Infrastructure --output schema.sql
```

## Modelo de datos

El detalle de cada tabla está en [docs/brief.md](docs/brief.md). Lo que conviene saber
para tocar el código:

**La normalización no es opcional.** Marca, modelo y versión son tablas con foreign keys y
con índices únicos, nunca texto libre. Si un vendedor pudiera escribir "VW", "Volkswagen" y
"volkswagen ", cualquier agregación posterior sería basura irrecuperable — y la analítica de
demanda es el producto, no el catálogo.

**Los tipos tampoco.** El año y el kilometraje son `int`, el precio es `decimal(12,2)` con la
moneda en columna aparte. En Uruguay se publica en dólares y en pesos: sin moneda explícita y
sin cotizaciones históricas no se puede comparar nada a lo largo del tiempo.

### Aislamiento entre tenants

Se sostiene sobre dos mecanismos, porque uno solo no alcanza:

1. **Lectura.** Toda entidad que implementa `ITenantEntity` tiene un filtro global en
   [`AppDbContext`](backend/Infrastructure/Persistence/AppDbContext.cs). Olvidarse un
   `WHERE tenant_id = ...` no alcanza para ver datos ajenos.
2. **Escritura.** Los filtros globales no tocan los `INSERT` ni los `UPDATE`. Por eso
   `SaveChanges` sella el tenant en las altas y rechaza cualquier escritura sobre datos de
   otro tenant, lanzando `TenantIsolationException`.

Sin tenant resuelto, las lecturas devuelven cero filas y las escrituras lanzan. Falla
cerrado a propósito: es la diferencia entre un bug de resolución que rompe una pantalla y
uno que filtra la base entera.

Los filtros están escritos uno por uno en `OnModelCreating` en vez de aplicarse por
reflexión, para que la frontera de seguridad pueda auditarse leyendo. La red contra olvidos
es un test que recorre todas las entidades `ITenantEntity` y falla si alguna quedó sin filtro.

El acceso cross-tenant del SuperAdmin existe, pero siempre explícito: `IgnoreQueryFilters()`
para leer y `PermitirEscrituraCrossTenant()` para escribir, únicamente desde los endpoints
bajo `/api/admin/*`. Nunca por un flag opcional en un endpoint normal.

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
