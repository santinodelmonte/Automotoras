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

> **Estado actual: fase 2 completa, salvo los dominios propios automatizados.** Sobre la
> fase 1 —sitio público por automotora, panel con ABM de vehículos y tablero, panel de
> SuperAdmin, tracking de eventos y jobs por endpoint— ahora están los reportes de
> demanda: qué se mira y no se consulta, qué se busca y no está, qué conviene comprar,
> precio de referencia de mercado y comparación anonimizada contra el resto de las
> automotoras. Lo único de la fase 2 que no está es la automatización de dominios propios;
> el porqué está en [docs/brief.md](docs/brief.md).

## Requisitos previos

| Herramienta | Versión | Notas |
| --- | --- | --- |
| .NET SDK | 8.0.x | La versión está fijada en [`global.json`](global.json). Si tenés instalado el SDK 10, igual se usa el 8. |
| Node.js | 20.19+ / 22.12+ | Probado con Node 24. |
| npm | 10+ | |
| MySQL | 8.0 | Necesario para aplicar migraciones y correr contra una base real. La API levanta y `/api/health` responde sin él. |
| MariaDB | 10.4+ | Alternativa para desarrollo: es lo que trae XAMPP. Hay que declararlo en `Database:ServerVersion` — ver abajo. |

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
- Swagger (solo en Development): <http://localhost:5080/swagger>, con el botón
  *Authorize* para pegar el `accessToken` que devuelve el login

**Hace falta `Jwt:Secret` para que arranque.** Sin clave de firma la API no levanta, y es
a propósito: una API que arranca igual y firma tokens con una clave vacía es peor que una
que no arranca. En desarrollo alcanza con un `appsettings.Development.json` (ignorado por
git) o con la variable de entorno:

```bash
Jwt__Secret="una-clave-larga-y-aleatoria-de-al-menos-32-chars" dotnet run
```

### La base en desarrollo

Con XAMPP, lo que corre no es MySQL sino MariaDB, y Pomelo genera SQL distinto para cada
una. La versión se declara en la configuración; el default es MySQL 8, que es lo que hay en
producción:

```json
"Database": { "ServerVersion": "10.4.32-mariadb" }
```

Sigue siendo declarada y no autodetectada: `ServerVersion.AutoDetect` abre una conexión
durante el arranque, y en IIS eso convierte una base momentáneamente caída en una aplicación
que no levanta. La versión que corre se ve con `SELECT VERSION();`.

Crear la base y aplicar las migraciones:

```bash
"C:/xampp/mysql/bin/mysql.exe" -u root -e "CREATE DATABASE IF NOT EXISTS automotora_saas CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"
```

```bash
ConnectionStrings__Default="Server=localhost;Port=3306;Database=automotora_saas;User Id=root;Password=;" Database__ServerVersion="10.4.32-mariadb" dotnet dotnet-ef database update --project backend/Infrastructure --startup-project backend/Infrastructure
```

### Usuarios de desarrollo

Definí `Seed:Password` y el arranque en Development siembra dos automotoras, sus usuarios
y el catálogo de marcas y modelos. Es idempotente: se puede correr en cada arranque. Sin
esa clave el seed no corre — no hay contraseña por defecto, porque una contraseña por
defecto que sobrevive a producción no la nota nadie hasta que es tarde.

| Usuario | Rol | Entra a |
| --- | --- | --- |
| `owner@norte.uy` | Owner | Todo lo de Automotora Norte, incluida la gestión de vendedores |
| `vendedor@norte.uy` | Seller | Vehículos y consultas de Automotora Norte |
| `owner@sur.uy` / `vendedor@sur.uy` | Owner / Seller | Lo mismo, en Automotora Sur |
| `super@automotoras.uy` | SuperAdmin | Cross-tenant, por `/api/admin/*` |

### Frontend

```bash
cd frontend && npm install && npm run dev
```

Queda escuchando en <http://localhost:5173>. Levantá el backend primero.

- `/admin/login` — login del panel privado
- `/admin` — panel, protegido por rol
- `/t/{slug}` — sitio público de una automotora (`/t/norte`, `/t/sur`)

### Tests

```bash
dotnet test
```

Los tests corren sobre SQLite en memoria, así que no hace falta MySQL para ejecutarlos. Se
eligió SQLite y no el proveedor `InMemory` a propósito: el `InMemory` no traduce a SQL y
evalúa los filtros con semántica de C#, con lo cual un filtro de tenant roto podría pasar
el test igual.

Hay dos niveles. Los de persistencia van contra el `DbContext` y prueban los filtros
globales y la política de escritura. Los de integración levantan la API entera con
`WebApplicationFactory` y van por HTTP: login, renovación, roles y aislamiento. La
diferencia importa — "la consulta filtra bien" y "el endpoint responde 404" no son lo
mismo, y lo único que le consta a quien está del otro lado es lo segundo.

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

## Autenticación y resolución de tenant

Es el cimiento: si esto queda mal, todo lo que se apoye encima hay que rehacerlo. Por eso
va antes que cualquier pantalla.

### Cómo se resuelve el tenant

[`ResolucionDeTenantMiddleware`](backend/Api/MultiTenancy/ResolucionDeTenantMiddleware.cs)
corre entre `UseAuthentication` y `UseAuthorization`, y tiene exactamente dos caminos que
no se cruzan:

1. **Panel privado.** El tenant sale del claim `tenant_id`, que está adentro de la firma
   del JWT. Si el request además trae un slug en la ruta, se ignora. Hay un test que lo
   comprueba: un Owner de Norte pidiendo `/t/sur/api/users` sigue viendo los usuarios de
   Norte.
2. **Sitio público.** Sin token, el tenant sale del `Host` (dominio propio) o del slug de
   `/t/{slug}`, siempre validado contra la tabla `tenants` y solo si está activo. Si no
   matchea, 404: no existe una automotora por defecto.

El slug se saca de la ruta y se pasa a `PathBase`, así que los controllers declaran su
ruta una sola vez y funcionan igual detrás de un dominio propio que detrás del slug de
desarrollo.

Fuera de esos dos casos el request queda sin tenant — y sin tenant no se lee ni se escribe
nada de ningún tenant.

### Tokens

- **Access token:** JWT firmado con HMAC-SHA256, quince minutos. Es sin estado y no se
  puede revocar; lo que lo acota es que venza rápido.
- **Refresh token:** 32 bytes aleatorios, opaco. En la base vive solo su SHA-256, así que
  si se filtra la tabla los tokens no son utilizables. Rota en cada uso: el que se canjea
  se quema. Presentar uno ya canjeado revoca todas las sesiones del usuario — si el token
  viejo reaparece, o se filtró o alguien está reproduciendo una sesión, y en los dos casos
  lo prudente es echar a todos.
- **Contraseñas:** PBKDF2-HMAC-SHA256, 210.000 iteraciones, sal por contraseña. El hash
  guardado declara algoritmo, costo y sal, así que subir el costo más adelante no invalida
  las contraseñas existentes.

### Roles

| Rol | Alcance |
| --- | --- |
| `SuperAdmin` | Cross-tenant, por endpoints separados bajo `/api/admin/*`. Su token no lleva tenant. |
| `Owner` | Todo dentro de su automotora, incluida la gestión de vendedores. |
| `Seller` | Vehículos y consultas. Sin reportes ni analítica. |

### Endpoints

**Sesión**

| Endpoint | Quién | Qué hace |
| --- | --- | --- |
| `POST /api/auth/login` | Anónimo | Abre sesión y devuelve el par de tokens |
| `POST /api/auth/refresh` | Anónimo | Rota el refresh token y renueva el access token |
| `POST /api/auth/logout` | Anónimo | Revoca el refresh token |
| `GET /api/auth/me` | Autenticado | El usuario de la sesión, armado con los claims |

**Panel de la automotora**

| Endpoint | Quién | Qué hace |
| --- | --- | --- |
| `GET/POST/PUT/DELETE /api/vehiculos` | Owner y Seller (borrar, solo Owner) | ABM de stock |
| `POST /api/vehiculos/{id}/estado` | Owner y Seller | Cambio rápido de estado |
| `/api/vehiculos/{id}/fotos` | Owner y Seller | Subir, reordenar, portada y borrar |
| `GET /api/catalogo/*` | Owner y Seller | Marcas, modelos, versiones y opciones |
| `POST /api/catalogo/solicitudes-modelo` | Owner y Seller | Pedir el alta de un modelo que falta |
| `GET/POST/PUT /api/users` | Owner | Vendedores de la automotora |
| `GET/PUT /api/tenant`, `POST /api/tenant/logo` | Owner | Identidad visual y contacto |
| `GET /api/dashboard` | Owner | Stock por estado y demanda de 30 días |

**Sitio público** — sin autenticación, con el tenant resuelto por dominio o slug

| Endpoint | Qué hace |
| --- | --- |
| `GET /api/public/tenant` | Identidad de la automotora |
| `GET /api/public/home` | Destacados, recientes y total, en un solo request |
| `GET /api/public/vehiculos` | Listado con filtros y paginación |
| `GET /api/public/filtros` | Solo lo que esta automotora tiene publicado |
| `GET /api/public/vehiculos/{id}` | Ficha, con el mensaje de WhatsApp ya armado |
| `POST /api/public/events` | Registro de eventos, con límite de tasa por IP |
| `GET /api/public/sitemap.xml` | Sitemap del tenant |

**SuperAdmin y jobs**

| Endpoint | Quién | Qué hace |
| --- | --- | --- |
| `GET/POST/PUT /api/admin/tenants` | SuperAdmin | ABM de automotoras, con su Owner |
| `POST /api/admin/tenants/{id}/verificar-dominio` | SuperAdmin | Comprueba que el dominio propio apunte acá y lo habilita |
| `/api/admin/catalogo/*` | SuperAdmin | ABM de marcas, modelos y versiones |
| `/api/admin/solicitudes-modelo` | SuperAdmin | Aprobar o rechazar altas de modelo |
| `POST /api/jobs/cotizaciones` | Cron externo | Cotización del día, con `X-Job-Secret` |
| `GET /api/jobs/modelos-a-cotizar` | Cron externo | Qué modelos y años están publicados, para no cotizar el catálogo entero |
| `POST /api/jobs/precios-de-mercado` | Cron externo | Snapshot diario de precios de referencia |

**Reportes de demanda** — solo Owner

| Endpoint | Qué hace |
| --- | --- |
| `GET /api/reportes/demanda` | Por unidad publicada: días en góndola, vistas, consultas, ratio y señal |
| `GET /api/reportes/busquedas-sin-resultado` | Las búsquedas vacías, agrupadas por lo que se buscaba |
| `GET /api/reportes/sugerencias` | Qué conviene comprar, cruzando esa demanda contra el stock |
| `GET /api/reportes/benchmark` | La automotora comparada contra la mediana del resto, anonimizada |

## Decisiones de fase 1

**Las fotos se achican en el navegador y se suben de a una.** Una foto de celular pesa
entre 3 y 8 MB; diez de esas por 4G son varios minutos y un buen riesgo de que se corte a
la novena y se pierdan las nueve. Redimensionadas a 1600 px quedan en unos 200 KB, más
resolución de la que cualquier galería web llega a mostrar. El servidor no procesa
imágenes: en shared hosting IIS esa CPU se le saca a todos los tenants a la vez.

**El precio de costo no sale del servidor hacia un Seller.** No está oculto en la
pantalla: viaja en `null`, y el endpoint tampoco lo acepta si lo manda un vendedor.

**El sitio público muestra solo lo disponible.** Los vendidos se mantienen en la base —son
la mitad de la historia de demanda— pero salen del listado, de la ficha y del sitemap en
el momento en que se marcan.

**El rango de precio exige moneda.** En Uruguay se publica en dólares y en pesos; un rango
que cruce las dos no significa nada, así que la API lo rechaza en vez de devolver un
listado sin sentido.

**Cada búsqueda con filtros queda registrada** con sus filtros y su cantidad de
resultados. Las que devuelven cero dejan además su propio evento: son la señal más valiosa
del producto, porque dicen qué le están pidiendo a la automotora que no tiene en stock. Un
listado sin filtros no se registra: sería ruido que después hay que descartar en cada
reporte.

## Decisiones de fase 2

**La señal de cada unidad se calcula en el servidor.** "Precio alto", "sin visibilidad" y
"estancado" son reglas de negocio con umbrales, declarados todos juntos en
[`UmbralesDeDemanda`](backend/Core/Reportes/ReporteDtos.cs). El panel las pinta, no las
decide: duplicar los umbrales en el cliente es garantizar que un día las dos pantallas
digan cosas distintas del mismo vehículo.

**Las búsquedas vacías se cuentan por visita, no por búsqueda.** Veinte búsquedas de una
sola persona indecisa no son demanda; veinte de veinte personas sí. Y hacen falta al menos
tres visitas distintas para que algo se convierta en una sugerencia de compra: con una
sugerencia por cada curioso, la pantalla es ruido y la primera buena se pierde en el medio.

**Una sugerencia cambia según el patio.** La misma búsqueda vacía significa "comprá" si no
hay una sola unidad de eso publicada, y "revisá precio, año o fotos" si hay tres. Sugerir
comprar cuando el stock ya existe es la forma más cara de equivocarse.

**El precio de mercado no lo sale a buscar la API.** Un barrido de precios son cientos de
llamadas salientes, y en shared hosting IIS cada una que se cuelga se lleva un hilo del app
pool que atiende a todos los tenants. El script vive en
[`tools/precios-de-mercado.mjs`](tools/precios-de-mercado.mjs), lo dispara el mismo cron que
los otros jobs y postea el lote. Un snapshot con menos de tres publicaciones se rechaza: un
precio de referencia equivocado es peor que ninguno, porque el que falta se nota y el que
está mal se cree.

**El precio de referencia se convierte a la moneda del aviso** con la cotización del día
del snapshot, no con la de hoy. Los dos números tienen que quedar parados en la misma fecha:
convertir un precio de mercado de la semana pasada al tipo de cambio de hoy mezcla la
diferencia de precio con la del dólar, y el porcentaje que sale no es ninguna de las dos. Sin
cotización aplicable no hay comparación, y se dice.

**La API de búsqueda de MercadoLibre ya no es abierta.** Responde 403 sin token, así que el
script necesita las credenciales de una aplicación creada en `developers.mercadolibre.com`
(`ML_CLIENT_ID` y `ML_CLIENT_SECRET`); pide el access token en cada corrida, porque uno
pegado a mano vence en horas y un cron diario lo encuentra siempre vencido.

**Un dominio propio no sirve el sitio hasta que se verifica.** Cargar un dominio es
declarar una intención; servirlo requiere haber comprobado que quien lo declaró lo
controla. La comprobación es que el dominio resuelva a alguna de las IP de
`Deploy:IpsPublicas`: para apuntar un dominio ahí hay que controlar su DNS, que es la
definición práctica de ser su dueño, y de paso es la condición que igual tiene que cumplirse
para que el sitio funcione. Sin la verificación, cualquier automotora puede escribir el
dominio de otra empresa en su configuración y quedárselo para el día en que ese dominio
apunte para acá. Cambiar el dominio invalida el sello anterior, y una verificación fallida
no le baja el sitio a quien ya lo tenía andando.

**El benchmark no se publica con muestra chica.** Hacen falta al menos cinco automotoras
además de la que pregunta, y lo que sale es una mediana entre automotoras — nunca un
extremo, nunca un nombre, nunca un id. Con dos competidores en un promedio, cada uno despeja
al otro con una resta, y en un mercado chico sabe perfectamente quiénes son. Es el único
endpoint de tenant que lee datos de otros tenants, y está solo en
[`BenchmarkController`](backend/Api/Controllers/BenchmarkController.cs) para que esa frontera
se pueda auditar abriendo un archivo.

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
| `Database:ServerVersion` | `Database__ServerVersion` | Versión del servidor, en el formato de Pomelo (`8.0.36-mysql`, `10.4.32-mariadb`). Sin valor, MySQL 8. |
| `Jwt:Issuer` / `Jwt:Audience` | `Jwt__Issuer` / `Jwt__Audience` | Emisor y audiencia de los tokens. |
| `Jwt:Secret` | `Jwt__Secret` | Clave de firma. **Obligatoria:** sin ella la API no arranca. Mínimo 32 caracteres, aleatoria. Nunca versionar. |
| `Jwt:AccessTokenMinutes` | `Jwt__AccessTokenMinutes` | Vida del access token. |
| `Jwt:RefreshTokenDays` | `Jwt__RefreshTokenDays` | Vida del refresh token. |
| `Storage:Provider` | `Storage__Provider` | `Local` en desarrollo, `R2` en producción. |
| `Storage:LocalRootPath` | `Storage__LocalRootPath` | Solo con `Provider=Local`: carpeta de subidas, fuera del repo. |
| `Storage:PublicBaseUrl` | `Storage__PublicBaseUrl` | URL pública desde la que se sirven las imágenes. |
| `Storage:Bucket` / `Storage:Endpoint` | `Storage__Bucket` / `Storage__Endpoint` | Bucket y endpoint S3-compatible (Cloudflare R2). |
| `Storage:AccessKeyId` / `Storage:SecretAccessKey` | `Storage__AccessKeyId` / `Storage__SecretAccessKey` | Credenciales del object storage. Nunca versionar. |
| `Jobs:Secret` | `Jobs__Secret` | Valor esperado en el header `X-Job-Secret` de `POST /api/jobs/{nombre}`. |
| `Deploy:IpsPublicas` | `Deploy__IpsPublicas__0` | IP públicas de la aplicación. Es contra lo que se verifica un dominio propio; sin ellas, ningún dominio se puede verificar. |
| `Analytics:IpHashSalt` | `Analytics__IpHashSalt` | Sal para hashear las IPs de los eventos. Si queda vacía se usa `Jwt:Secret`. |
| `Seed:Password` | `Seed__Password` | Contraseña de los usuarios de desarrollo. Solo se usa en Development; sin valor, el seed no corre. |
| `Cors:AllowedOrigins` | `Cors__AllowedOrigins__0` | Orígenes del frontend habilitados. En desarrollo, `http://localhost:5173`. |

### Frontend

Copiá [`frontend/.env.example`](frontend/.env.example) a `frontend/.env`:

| Variable | Para qué |
| --- | --- |
| `VITE_API_BASE_URL` | Base URL de la API. En desarrollo, `http://localhost:5080`. Sin valor se asume el mismo origen, que es el default correcto en producción. |

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
