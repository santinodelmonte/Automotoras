# Brief — SaaS multi-tenant para automotoras

> Documento fuente del proyecto. Define el alcance, la arquitectura y el orden de trabajo.
> El Paso 0 (esqueleto ejecutable) ya está implementado; el resto es contexto e intención.

## Contexto

Construir un SaaS multi-tenant donde cada automotora tiene su propio sitio público de
venta de vehículos y un panel de administración privado. El diferencial del producto no
es el catálogo (eso es commodity), sino la **inteligencia de demanda**: medir qué miran
los compradores, qué consultan, qué buscan y no encuentran, y cuánto tiempo queda cada
vehículo en góndola, para que el dueño de la automotora compre stock con datos y no con
intuición.

**Decisión de arquitectura fundamental:** una sola aplicación, una sola base de datos,
multi-tenant. Los datos agregados son el activo del producto; separar bases por cliente
destruiría la propuesta de valor. La identidad de cada automotora (dominio, logo,
colores) se resuelve por configuración de tenant, no por deploy separado.

## Stack

- **Backend:** ASP.NET Core 8 Web API (C#), Entity Framework Core, MySQL (Pomelo provider)
- **Frontend:** React + TypeScript + Vite + Tailwind CSS
- **Auth:** JWT (access token + refresh token)
- **Storage de imágenes:** object storage S3-compatible (Cloudflare R2), abstraído detrás
  de una interfaz `IImageStorage` con una implementación local para desarrollo
- **Deploy inicial:** SmarterASP.NET (shared hosting Windows/IIS)

### Restricciones de deploy que el código debe respetar

- Nunca escribir archivos en el disco del servidor. Todo binario va a object storage.
- No usar `IHostedService` / `BackgroundService` para trabajo crítico. El app pool de IIS
  recicla impredeciblemente. Los jobs periódicos se exponen como endpoints
  `POST /api/jobs/{nombre}` protegidos por un header `X-Job-Secret`, disparables por un
  cron externo.
- Toda configuración (connection string, claves, secretos, URLs de storage) por variables
  de entorno / `appsettings` sobrescribible. Nada hardcodeado.

## Estructura del repositorio

Monorepo:

```
/backend
  /Api                 (controllers, middleware, DI, Program.cs)
  /Core                (entidades, interfaces, DTOs, lógica de dominio)
  /Infrastructure      (DbContext, repositorios, EF migrations, storage, servicios externos)
  /Tests               (xUnit)
/frontend
  /src
    /admin             (panel privado)
    /public            (sitio público del tenant)
    /shared            (componentes, hooks, api client, tipos)
/docs
```

> **Avance:** pasos 0 (esqueleto), 2 (modelo de datos), 3 (autenticación, roles y
> resolución de tenant) y 4 (features de fase 1) están hechos. Lo que sigue es la fase 2:
> los reportes de demanda, que los datos que ya se están acumulando alimentan.
>
> Desvíos respecto de este documento, decididos durante la implementación:
> - Se agregó la tabla `solicitudes_modelo`, que no está en la lista de tablas pero es
>   necesaria para la aprobación de altas de modelos que sí es feature de fase 1.
> - La entidad de `versiones` se llama `VersionVehiculo` en C# para no chocar con
>   `System.Version`.
> - `vehiculo_fotos` no lleva `tenant_id` propio, como dice el brief; su filtro global
>   navega hasta el tenant del vehículo.
> - El slug del sitio público viaja como prefijo de ruta (`/t/{slug}/api/public/...`) y el
>   middleware lo pasa a `PathBase`. Así los controllers declaran su ruta una sola vez y
>   funcionan igual detrás de un dominio propio que detrás del slug de desarrollo.
> - El aislamiento de escritura se extendió a `users`, que no implementa `ITenantEntity`
>   porque su tenant es anulable. Sin eso, la gestión de vendedores era el único lugar
>   donde un `tenant_id` ajeno pasaba sin que nadie lo mirara.
> - El endpoint de gestión de usuarios (`/api/users`) se adelantó al paso 3: es el recurso
>   de tenant que hacía falta para escribir el test de aislamiento end-to-end que pide el
>   criterio de aceptación número uno.
> - El `session_id` del tracking lo genera y guarda el cliente en `localStorage`, no una
>   cookie de primera parte como pide el brief. El sitio y la API viven en orígenes
>   distintos —y con dominio propio por automotora eso no cambia—, así que una cookie
>   puesta por la API sería de tercera parte: los navegadores la bloquean por defecto y el
>   dato se perdería justo donde más tráfico hay.
> - El mensaje de WhatsApp dice "que vi en la web" y no "que vieron en la web": lo escribe
>   el comprador, no la automotora.
> - Las fotos se suben de a una y ya achicadas por el navegador. Un solo request con diez
>   imágenes de celular es lo que hace que la carga desde el teléfono termine en timeout,
>   que es justamente el criterio de aceptación número dos.
> - El sitio público muestra únicamente los vehículos `Disponible`. El brief solo pide que
>   salgan los vendidos, pero el DTO público no tiene dónde decir "reservado", y mostrar
>   como disponible algo que no lo está le hace perder el viaje al comprador.
> - Los reportes SEO por vehículo se resuelven con meta tags puestos desde el cliente y un
>   sitemap por tenant servido por la API. Los crawlers de Open Graph que no ejecutan
>   JavaScript necesitan prerenderizado, que no es parte de la fase 1: queda anotado.
> - El job de cotizaciones recibe el valor en el cuerpo del request en vez de salir a
>   buscarlo. En shared hosting IIS una llamada saliente colgada se lleva un hilo del app
>   pool que atiende a todos los tenants, y el cron externo ya tiene que existir igual.

## Paso 0 — Esqueleto ejecutable ✅ hecho

**Regla:** la primera entrega es únicamente el andamiaje del proyecto. Nada de lógica de
negocio, nada de entidades, nada de features. El objetivo es tener un repositorio que
compile, corra y se pueda subir a GitHub para trabajar desde ahí.

### Qué crear

**Backend** — solución .NET con los cuatro proyectos y sus referencias ya cableadas:

```
dotnet new sln -n AutomotoraSaaS
dotnet new webapi   -o backend/Api
dotnet new classlib -o backend/Core
dotnet new classlib -o backend/Infrastructure
dotnet new xunit    -o backend/Tests
```

Referencias: `Api → Core, Infrastructure`; `Infrastructure → Core`;
`Tests → Core, Infrastructure`. `Core` no referencia a nadie.

Contenido mínimo del backend:

- Un único endpoint `GET /api/health` que devuelva `{ "status": "ok", "timestamp": "..." }`
- Swagger habilitado en Development
- CORS configurado para el origen del frontend en desarrollo
- `appsettings.json` con las claves vacías que va a necesitar el proyecto (connection
  string, JWT secret, storage) y un `appsettings.Example.json` versionado que documente
  la forma esperada
- Un test en `Tests` que verifique que `/api/health` responde 200. Sirve para confirmar
  que el pipeline de tests funciona antes de que haya algo que testear.

**Frontend** — proyecto Vite con React + TypeScript y Tailwind configurado:

```
npm create vite@latest frontend -- --template react-ts
```

Contenido mínimo del frontend:

- Tailwind instalado y funcionando (un elemento con clases de Tailwind que se vea
  claramente estilado)
- Una pantalla única que llame a `GET /api/health` y muestre el resultado en pantalla
- Cliente de API tipado mínimo en `src/shared/api/client.ts`, con la base URL leída de
  variable de entorno
- Las carpetas `src/admin`, `src/public` y `src/shared` creadas, aunque estén casi vacías
- `.env.example` versionado, `.env` ignorado

**Archivos de repositorio**

- `.gitignore` que cubra ambos stacks: `bin/`, `obj/`, `.vs/`, `*.user`, `node_modules/`,
  `dist/`, `.env`, y explícitamente `appsettings.Development.json` y
  `appsettings.Production.json`
- `README.md` con: qué es el proyecto en dos párrafos, requisitos previos, cómo levantar
  backend y frontend, y las variables de entorno necesarias
- `.editorconfig` con las convenciones de C# y TypeScript
- `docs/` con este brief adentro

### Criterio de aceptación del Paso 0

1. `dotnet build` compila sin errores ni warnings
2. `dotnet test` pasa
3. El backend levanta y `GET /api/health` responde 200 desde el navegador
4. El frontend levanta, consume ese endpoint y muestra el resultado en pantalla con
   estilos de Tailwind aplicados
5. `git status` no muestra ni un archivo que no deba estar versionado — sin
   `node_modules`, sin `bin/`, sin secretos

No avanzar a las secciones siguientes de este documento hasta que los cinco puntos se
cumplan. El resto del brief describe hacia dónde va el proyecto y sirve como contexto
para las decisiones de estructura, pero no se implementa todavía.

## Multi-tenancy y seguridad

Este es el punto donde un error se paga caro. Reglas no negociables:

1. **Panel privado:** el `tenantId` se resuelve exclusivamente desde un claim del JWT.
   Jamás desde un header, query param o body enviado por el cliente. Un usuario
   autenticado de la automotora A no puede, bajo ninguna manipulación de request, leer o
   escribir datos de la automotora B.
2. **Sitio público:** el tenant se resuelve desde el `Host` header (dominio custom) o
   desde un slug en la ruta (`/t/{slug}` en desarrollo), y siempre se valida contra la
   tabla `tenants`. Si no matchea, 404.
3. **Global query filters de EF Core:** todas las entidades que pertenecen a un tenant
   implementan `ITenantEntity { int TenantId { get; set; } }`, y el `DbContext` aplica un
   `HasQueryFilter` global que filtra por el tenant del contexto de request. Esto hace que
   olvidarse un `WHERE` sea imposible por defecto, no una cuestión de disciplina.
4. **Rol superadmin:** puede operar cross-tenant, pero por endpoints explícitamente
   separados bajo `/api/admin/*`, con el filtro global deshabilitado de forma consciente y
   auditada. Nunca por un flag opcional en los endpoints normales.

### Roles

- `SuperAdmin` — cross-tenant, gestión de tenants, alta de marcas/modelos, métricas globales
- `Owner` — dueño de la automotora: todo dentro de su tenant, incluyendo reportes,
  analítica y gestión de vendedores
- `Seller` — vendedor: alta/edición de vehículos, ver consultas. Sin acceso a reportes ni
  analítica.

## Modelo de datos

### Normalización — crítico para que la analítica exista

Marca, modelo y versión van en tablas propias con foreign keys. **Bajo ninguna
circunstancia se guardan como texto libre.** Si el vendedor puede escribir "VW",
"Volkswagen" y "volkswagen ", cualquier agregación posterior es basura irrecuperable. El
formulario de carga usa selects encadenados (marca → modelo → versión) alimentados desde
la base. Si falta un modelo, el vendedor solicita el alta y el SuperAdmin la aprueba; no
lo crea libremente.

### Tablas

`tenants` — id, slug (único), nombre, dominio_custom (nullable, único), logo_url,
color_primario, color_secundario, whatsapp, telefono, direccion, activo, created_at

`users` — id, tenant_id (nullable para SuperAdmin), email (único), password_hash, nombre,
rol, activo, created_at

`refresh_tokens` — id, user_id, token_hash, expira_en, revocado_en

`marcas` — id, nombre, activo
`modelos` — id, marca_id, nombre, carroceria (enum), activo
`versiones` — id, modelo_id, nombre, activo

`vehiculos` — id, tenant_id, modelo_id, version_id (nullable), anio (int), kilometraje
(int), combustible (enum: Nafta, Diesel, Hibrido, Electrico, GNC), transmision (enum:
Manual, Automatica), color, puertas, motor, precio (decimal), moneda (enum: USD, UYU),
estado (enum: Disponible, Reservado, Vendido, Pausado), descripcion, destacado (bool),
precio_costo (decimal, nullable, solo visible para Owner y SuperAdmin),
fecha_publicacion, fecha_venta (nullable), precio_venta (nullable), created_at, updated_at

El kilometraje es `int`, el año es `int`, el precio es `decimal` con moneda en columna
aparte. Nunca texto. En Uruguay se publica tanto en dólares como en pesos: sin la moneda
explícita y sin cotizaciones históricas, no se puede comparar nada a lo largo del tiempo.

`vehiculo_fotos` — id, vehiculo_id, url, url_thumb, orden, es_portada

`eventos` — id, tenant_id, vehiculo_id (nullable), tipo (enum: ViewFicha, ViewListado,
ClickWhatsapp, ClickTelefono, BusquedaSinResultado), session_id, ip_hash, user_agent,
referer, created_at, metadata (JSON)

`busquedas` — id, tenant_id, filtros (JSON), resultados_count, session_id, created_at

`cotizaciones` — id, fecha, usd_uyu (decimal) — una fila por día, poblada por job

**Índices obligatorios:** `eventos(tenant_id, vehiculo_id, tipo, created_at)`,
`vehiculos(tenant_id, estado)`, `vehiculos(modelo_id, anio)`. La tabla de eventos crece
rápido y es la que alimenta todos los reportes.

## Tracking de eventos

Se instrumenta desde el primer día, incluso antes de que exista un solo reporte. Los datos
de demanda solo tienen valor acumulados en el tiempo; lo que no se mide hoy no se recupera
nunca.

Eventos a capturar en el sitio público:

- Vista de ficha de vehículo
- Clic en botón de WhatsApp (con el vehículo asociado)
- Clic en teléfono
- Búsqueda ejecutada, con los filtros aplicados y la cantidad de resultados
- Búsqueda con cero resultados (evento propio, es la señal más valiosa: dice qué le están
  pidiendo a la automotora que no tiene en stock)

**Implementación:** endpoint `POST /api/public/events`, sin autenticación pero con rate
limiting por IP y validación de que el tenant y el vehículo existen y se corresponden.
`session_id` en un cookie de primera parte con una duración razonable. La IP se guarda
hasheada, no en claro.

**Privacidad entre tenants:** ningún tenant puede ver datos identificables de otro. Los
benchmarks comparativos que se expongan a futuro deben ser agregados y anonimizados, con
un mínimo de N registros para publicarse.

## Alcance por fases

### Fase 1 — construir ahora ✅ hecha

**Sitio público (por tenant):**

- Home con vehículos destacados y branding del tenant
- Listado con filtros: marca, modelo, año (rango), precio (rango), kilometraje (rango),
  combustible, transmisión, carrocería
- Ficha de vehículo: galería de fotos, ficha técnica completa, botón de WhatsApp con
  mensaje prearmado (`Hola, me interesa el {marca} {modelo} {año} que vieron en la web`) y
  botón de teléfono
- Los vehículos vendidos salen del listado; se mantienen en la base
- Responsive mobile-first: la mayoría del tráfico va a ser de celular
- SEO: meta tags y Open Graph por vehículo, sitemap por tenant

**Panel de administración:**

- Login con JWT
- CRUD de vehículos con carga múltiple de fotos, reordenamiento y selección de portada
- Cambio rápido de estado (Disponible / Reservado / Vendido / Pausado). Al marcar Vendido,
  pide fecha y precio de venta.
- Gestión de usuarios (Owner puede crear Sellers)
- Configuración del tenant: logo, colores, datos de contacto
- Dashboard básico: cantidad de vehículos por estado, vistas de los últimos 30 días, top 5
  vehículos más vistos

**Panel de SuperAdmin:**

- CRUD de tenants
- CRUD de marcas, modelos y versiones
- Aprobación de solicitudes de alta de modelos

### Fase 2 — no construir todavía, pero dejar el modelo de datos preparado

- Reportes de demanda: días en góndola por vehículo, ratio consultas/vistas, vehículos con
  muchas vistas y pocas consultas (señal de precio alto), búsquedas sin resultados
  agrupadas
- Sugerencias de compra basadas en demanda insatisfecha
- Precio de referencia de mercado vía API pública de MercadoLibre, con snapshots diarios en
  tabla propia
- Custom domains automatizados
- Benchmarks anonimizados cross-tenant

**No implementar en fase 1:** pagos, financiación, permutas, integración con MercadoLibre,
notificaciones por email, chat en vivo.

## Convenciones

- **Backend:** DTOs separados de entidades (nunca exponer entidades EF en la API).
  Validación con FluentValidation. Respuestas de error consistentes con ProblemDetails.
- **Frontend:** cliente de API tipado, interceptor que agrega el JWT y maneja el refresh en
  401. Rutas protegidas por rol. Nada de `any`.
- **Migraciones** EF Core versionadas en el repo. Seed data para desarrollo: 3 tenants,
  marcas y modelos reales del mercado uruguayo, ~30 vehículos con eventos sintéticos
  distribuidos en los últimos 90 días (para poder ver los reportes con datos plausibles).
- **Tests unitarios obligatorios** para: resolución de tenant, filtros globales de query, y
  aislamiento entre tenants (un test que verifique explícitamente que el usuario del tenant
  A recibe 404 al pedir un recurso del tenant B).

## Criterios de aceptación de la fase 1

1. Un usuario del tenant A no puede acceder a ningún dato del tenant B por ninguna vía,
   incluyendo manipulación directa de IDs en la URL. Hay un test que lo prueba.
2. Cargar un vehículo con 10 fotos desde el celular funciona sin timeout.
3. El sitio público de cada tenant se ve con su branding y carga en menos de 3 segundos en 4G.
4. Los eventos de vista y de clic en WhatsApp se registran correctamente y son consultables
   por vehículo.
5. Marcar un vehículo como vendido lo saca del sitio público de inmediato y registra fecha y
   precio de venta.
6. El proyecto corre localmente con un solo comando por lado (backend y frontend) y una
   connection string en variable de entorno.

## Cómo trabajar

Orden estricto:

1. **Paso 0** — esqueleto ejecutable y subible a GitHub. Nada más.
2. **Modelo de datos**, entidades y migraciones iniciales.
3. **Autenticación, roles y resolución de tenant**, con los tests de aislamiento.
4. **Features de fase 1.**

La resolución de tenant es el cimiento: si queda mal, todo lo que se apoye encima hay que
rehacerlo. Por eso va antes que cualquier pantalla.
