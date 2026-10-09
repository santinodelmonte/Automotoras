# Fase 3 — Lo que falta para poder vender el sistema

> Documento de trabajo. Define qué hay que construir para que el producto pueda entregarse
> a un cliente que paga, tomando como contrato lo que promete la propuesta comercial
> ([docs/comercial/](comercial/)).
>
> El brief original ([docs/brief.md](brief.md)) describe el producto. Este documento
> describe el negocio alrededor del producto: planes, límites, vencimientos, alta y baja de
> clientes. Nada de esto existe hoy en el código.

## Contexto

La propuesta comercial vende tres planes —Vidriera, Demanda y Full— con precios, límites y
condiciones concretas. El sistema no sabe nada de eso: la entidad `Tenant` tiene slug,
nombre, branding, contacto y un booleano `Activo`, y ahí termina. No hay plan, no hay
vencimiento, no hay tope de vehículos ni de usuarios, y no hay forma de saber si una
automotora está al día.

Mientras haya un solo cliente eso se maneja de memoria. Con tres ya no, y el momento de
descubrirlo no puede ser el mes en que uno deja de pagar y sigue publicando.

**Decisión de alcance:** el cobro es manual y vive fuera del sistema. El sistema no cobra:
sabe qué plan tiene cada automotora, hasta cuándo está paga y qué puede hacer con eso. Una
pasarela de pago es un proyecto propio (integración, webhooks, reintentos, conciliación) y
no se justifica hasta tener una cantidad de clientes que haga doloroso el cobro manual.

## Qué promete la propuesta y qué hay hoy

| Promesa | Estado | Dónde |
| --- | --- | --- |
| Tres planes con precios distintos | **No existe** | Nada en el modelo de datos |
| Tope de vehículos publicados (40 / 120 / sin límite) | **No existe** | `VehiculosController` no consulta ningún tope |
| Tope de usuarios (2 / 6 / ilimitados) | **No existe** | `UsersController` no consulta ningún tope |
| Reportes de demanda solo en Demanda y Full | **No existe** | `ReportesController` no discrimina por plan |
| Benchmark solo en Full | **No existe** | `BenchmarkController` no discrimina por plan |
| "Dos primeros meses sin costo" y vencimiento del abono | **No existe** | No hay fechas de suscripción |
| Dominio propio en Demanda y Full | **A medio camino** | Verificación hecha; falta TLS y binding |
| "Se puede cambiar de plan en cualquier momento" | **No existe** | — |
| "Baja avisando con 30 días, sin permanencia" | **No existe** | Solo el booleano `Activo`, que nadie usa |
| "Los datos son suyos; ante una baja se entrega una exportación completa" | **No existe** | No hay ningún endpoint de exportación |
| "Respaldos periódicos de la información" | **No verificado** | Depende del hosting, sin procedimiento escrito |
| "Soporte con tiempos de respuesta por plan" | **No existe** | No hay canal ni registro de pedidos |
| Sitio público, panel, tracking, reportes, precio de referencia | **Hecho** | Fases 1 y 2 |

Lo que sigue son los pasos para cerrar esa tabla, en orden de dependencia.

---

## Paso 1 — Planes y suscripciones en el modelo de datos

> **Hecho.** Migración `PlanesYSuscripciones`. Las automotoras existentes pasan al plan Full
> con los dos meses bonificados. El alta por `/api/admin/tenants` acepta un `plan` opcional
> (por defecto, Demanda) y crea la suscripción junto con la automotora. La regla de "una sola
> vigente" la garantiza un índice único sobre una columna generada, no el código.

Es el cimiento: todo lo demás lee de acá. Va primero y solo.

### Entidades

**`Plan`** — el catálogo de planes, en la base y no en una constante del código. Los precios
cambian, los topes cambian, y una promoción no puede exigir un deploy.

```
id, codigo (vidriera|demanda|full, único), nombre,
precio_mensual (decimal), moneda,
max_vehiculos (int?, nulo = sin límite),
max_usuarios (int?, nulo = sin límite),
incluye_reportes (bool), incluye_benchmark (bool), incluye_dominio_propio (bool),
horas_soporte_mes (int), activo (bool), created_at
```

Los tres planes de la propuesta se siembran con una migración de datos, no con el seed de
desarrollo: en producción tienen que existir igual.

**`Suscripcion`** — qué plan tiene un tenant y desde cuándo. Es una tabla de historial, no
una columna en `tenants`: hace falta saber en qué plan estaba una automotora en marzo, y
un campo que se pisa no lo dice.

```
id, tenant_id, plan_id,
inicio (date), fin (date?, nulo mientras está vigente),
paga_hasta (date),        -- hasta cuándo está cubierta; es el corazón del vencimiento
motivo_de_baja (string?), created_at
```

Un tenant tiene como mucho una suscripción con `fin` nulo. Esa es la vigente.

**`Pago`** — el registro manual de cada cobro.

```
id, tenant_id, suscripcion_id, fecha (date), monto (decimal), moneda,
periodo_desde (date), periodo_hasta (date),
medio (string), comprobante (string?), nota (string?), created_at
```

Registrar un pago empuja `paga_hasta` de la suscripción hasta `periodo_hasta`. Los dos
meses bonificados se cargan como un pago de monto cero con la nota correspondiente: así el
descuento queda documentado y no como un hueco inexplicable en el historial.

### Criterio de aceptación

- Migración aplicada, con los tres planes sembrados.
- Ningún tenant queda sin suscripción: la migración le asigna una a cada tenant existente.
- Tests de que un tenant no puede tener dos suscripciones vigentes a la vez.

---

## Paso 2 — Panel de SuperAdmin: planes, suscripciones y cobranza

> **Hecho.** `AdminCobranzaController` y la pantalla **Cobranza** del panel: contadores por
> estado, lista ordenada por urgencia, cobro, cambio de plan, baja, historial y editor de
> planes. Cambiar de plan cierra la vigente y respeta lo ya pagado.

Es la pantalla que hoy no existe y que hace falta desde el primer cliente que paga.

### Endpoints (`/api/admin/*`, solo SuperAdmin)

- `GET /api/admin/planes`, `POST`, `PUT` — ABM de planes.
- `GET /api/admin/tenants/{id}/suscripcion` — la vigente más el historial.
- `POST /api/admin/tenants/{id}/suscripcion` — asignar o cambiar de plan.
- `POST /api/admin/tenants/{id}/pagos` — registrar un cobro.
- `POST /api/admin/tenants/{id}/baja` — cerrar la suscripción con motivo y fecha.
- `GET /api/admin/cobranza` — el tablero: todas las automotoras con su plan, `paga_hasta`,
  días para vencer y estado. Ordenable por vencimiento.

### Pantalla

Una página nueva en el panel de SuperAdmin (`frontend/src/admin/paginas/`), al lado de
`AutomotorasPage`. Lo primero que tiene que mostrar, sin filtrar ni buscar nada, es **quién
vence esta semana y quién está vencido**. Todo lo demás es secundario.

Por automotora: plan actual, `paga_hasta`, estado, uso real contra los topes del plan
(vehículos publicados y usuarios), y el historial de pagos.

### Criterio de aceptación

- Un SuperAdmin puede dar de alta una automotora, asignarle plan, registrar el primer pago
  y ver cuándo vence, sin tocar la base a mano.
- El tablero de cobranza muestra los vencimientos ordenados por urgencia.
- Tests de que ninguno de estos endpoints es accesible con rol Owner o Seller.

---

## Paso 3 — Límites del plan, aplicados de verdad

> **Hecho.** `IPoliticaDePlan` en `Core`, consultada desde vehículos, usuarios y dominios;
> reportes y benchmark con `[RequiereDelPlan]`. El rechazo es un 403 con `type:
> limite-del-plan`, el tope y el uso. Aviso en el panel desde el 80 % y pantalla **Mi plan**
> para el dueño. Reactivar un usuario o volver a publicar una unidad pausada también cuentan.

Un tope que no se aplica es una promesa de venta que no se cumple al revés: el cliente paga
Vidriera y usa Full.

### Dónde se aplica

- **Vehículos:** al crear uno, y al pasar uno a `Disponible` desde otro estado. El tope
  cuenta unidades **publicadas** (`Disponible` y `Reservado`), no filas en la tabla: un
  vendido no ocupa lugar en la vidriera y cobrarle por eso al cliente es indefendible.
- **Usuarios:** al crear un usuario en `UsersController`.
- **Reportes:** `ReportesController` requiere `incluye_reportes`.
- **Benchmark:** `BenchmarkController` requiere `incluye_benchmark`.
- **Dominio propio:** cargar un dominio requiere `incluye_dominio_propio`.

### Cómo se aplica

Límite duro: la operación se rechaza. Pero el error importa tanto como el bloqueo. Un 403
seco manda al cliente a llamar por teléfono enojado; la respuesta tiene que decir cuál es
el tope, cuánto está usando y que se resuelve cambiando de plan.

Se implementa como un servicio `IPoliticaDePlan` en `Core`, consultado desde los
controllers, y no como middleware: el middleware no sabe si la operación en curso publica
un vehículo o solo lo edita, y esa distinción es justamente el límite.

**El panel avisa antes de bloquear.** A partir del 80 % del tope, un cartel en el panel
dice cuánto queda. Que el cliente se entere del techo cuando lo choca es una mala forma de
vender un upgrade.

### Criterio de aceptación

- Tests por cada tope: el vehículo 41 en Vidriera se rechaza con un error que nombra el
  tope y el uso actual.
- Un Owner de plan Vidriera recibe 403 en los endpoints de reportes.
- Bajar de plan estando por encima del tope no rompe nada: no se borra nada, se bloquea
  publicar más hasta volver debajo del límite. El panel lo explica.

---

## Paso 4 — Vencimiento, gracia y suspensión

> **Hecho.** `CicloDeCobro.Evaluar` es la función pura; los umbrales están en `Cobranza:*`.
> El sitio suspendido responde 503 con `Retry-After` y el frontend muestra mantenimiento.
> `POST /api/jobs/avisos-de-vencimiento` avisa por SMTP (`Correo:*`) y registra cada aviso
> en `avisos_de_cobro`, uno por etapa y por vencimiento. **Falta configurar el SMTP real**.

El ciclo completo, que hoy no existe:

1. **Vigente** — `paga_hasta` en el futuro. Todo funciona.
2. **Por vencer** — faltan 7 días o menos. Aviso en el panel del Owner; el sitio público
   sin cambios.
3. **Gracia** — vencido hace 10 días o menos. Aviso más visible en el panel. **El sitio
   público sigue arriba.** Una automotora que se atrasa tres días no puede quedarse sin
   vidriera un sábado a la mañana: el daño para el cliente es enorme y el ahorro para
   nosotros es cero.
4. **Suspendido** — pasada la gracia. El sitio público responde una página de
   mantenimiento, no un 404 ni un error: la dirección web es del cliente y su reputación
   también. El panel sigue accesible para que puedan ver sus datos, exportarlos y
   regularizar.
5. **Reactivación** — registrar un pago vuelve a Vigente de inmediato, sin intervención.

Los umbrales (7 días de aviso, 10 de gracia) van en configuración, no hardcodeados.

### Implementación

El cálculo del estado es una función pura sobre `paga_hasta` y la fecha de hoy: no hay job
que "marque" vencidos, porque un job que no corre deja el estado mintiendo. La suspensión
se evalúa en la resolución de tenant, que ya existe.

Sí hace falta un job para **avisar**: `POST /api/jobs/avisos-de-vencimiento`, protegido por
`X-Job-Secret` como los demás (nada de `BackgroundService`, por la restricción de IIS que
ya está documentada en el brief). Manda el aviso al Owner y deja registro de que se mandó.

### Criterio de aceptación

- Tests de los cinco estados con fechas límite exactas, incluido el día del vencimiento.
- Un tenant suspendido devuelve mantenimiento en el sitio público y 200 en el panel.
- Registrar un pago de un tenant suspendido lo reactiva sin pasos manuales.

---

## Paso 5 — Exportación de datos

> **Hecho.** `GET /api/tenant/exportacion`, botón en **Mi plan**. ZIP en memoria con
> vehículos, fotos, consultas y eventos por día, búsquedas sin resultado y un LEEME. Los
> textos se neutralizan contra inyección de fórmulas al abrirse en Excel.

La propuesta dice, textualmente, que los datos son de la automotora y que ante una baja se
entrega una exportación completa del stock y del histórico de demanda. Hoy no hay forma de
hacerlo salvo entrando a la base.

- `GET /api/tenant/exportacion` — disponible para el Owner, en cualquier momento y no solo
  ante una baja. Devuelve un ZIP con CSVs: vehículos con todos sus campos y estados, fotos
  (URLs), consultas, eventos agregados por vehículo y por día, y búsquedas sin resultados.
- Los eventos van agregados y no crudos: el crudo incluye el `session_id` de cada visita,
  que no le sirve a nadie fuera de los reportes.
- Generación en memoria y descarga directa. **Nada se escribe en el disco del servidor**,
  por la restricción de deploy que ya rige en todo el proyecto.

### Criterio de aceptación

- El ZIP se genera sin escribir archivos temporales.
- Un Owner exporta únicamente los datos de su tenant. Hay un test que lo prueba.
- Un tenant suspendido puede exportar. Es la situación exacta que la promesa contempla.

---

## Paso 6 — Dominios propios, la mitad que falta

> **Decidido: alta manual por cliente.** Procedimiento en
> [operacion.md](operacion.md#2-dominios-propios-paso-6). Además, cargar un dominio ahora
> requiere un plan que lo incluya. Falta anotar los pasos exactos del panel del hosting la
> primera vez que se haga.

La verificación de dominio está hecha: un dominio no sirve el sitio hasta que se comprueba
que apunta a la aplicación, la comprobación es un endpoint de SuperAdmin y cambiar el
dominio invalida el sello. Lo que falta es emitir el certificado TLS y dar de alta el
binding en el servidor web.

**Esto no es una tarea de código, es una decisión de infraestructura**, y hay que tomarla
antes de escribir nada: SmarterASP.NET no tiene API para eso; se hace desde su panel. Dos
caminos:

| Opción | Qué implica | Costo |
| --- | --- | --- |
| **Proxy con TLS automático adelante** (Cloudflare u otro) | El proxy termina TLS y la app sigue donde está. Es el cambio más chico y resuelve el certificado solo. Hay que revisar cómo llega el host real a la resolución de tenant. | Bajo |
| **Mover el hosting** a un VPS o a un PaaS con certificados automáticos | Resuelve el problema de raíz y levanta de paso la restricción de no usar `BackgroundService`. Es migrar todo el deploy. | Alto |
| **Alta manual por cliente** desde el panel del hosting | Cero desarrollo. Con dos o tres clientes es perfectamente viable. | Cero, hasta que deja de serlo |

**Recomendación:** con la cantidad de clientes que la propuesta contempla, la tercera. Se
documenta el procedimiento como parte del onboarding y se pasa a la primera cuando el alta
manual empiece a molestar. Lo importante es que la promesa del plan Demanda se cumpla en
la semana 4, y para eso alcanza.

Si se elige el proxy, la tarea de código es asegurar que la resolución de tenant lea el
host correcto detrás del proxy (`X-Forwarded-Host`) y que eso no se pueda falsificar desde
afuera para hacerse pasar por otro tenant. Ese último punto tiene test obligatorio.

---

## Paso 7 — Onboarding de una automotora nueva

> **Hecho.** Carga masiva por CSV con plantilla y validación fila por fila, todo o nada,
> para el dueño y para el SuperAdmin desde Cobranza. El alta elige plan y carga colores y
> contacto. La contraseña del alta es provisoria: con ella la API solo permite cambiarla
> (`POST /api/auth/password`), y el panel muestra únicamente ese formulario. Lo mismo para
> los vendedores que da de alta el dueño. El logo se sube desde Configuración. Checklist
> de salida en vivo en [operacion.md](operacion.md#1-alta-de-una-automotora-nueva-paso-7).

Hoy dar de alta un cliente es una serie de pasos que solo están en la cabeza de quien
desarrolló el sistema. La propuesta promete salida en vivo en cuatro semanas, y eso exige
que el alta sea un procedimiento y no una arqueología.

- Un asistente en el panel de SuperAdmin: datos de la automotora, plan, branding, usuario
  Owner inicial y contraseña de un solo uso.
- **Carga masiva de stock por CSV**, con una plantilla y validación previa que muestra los
  errores fila por fila antes de escribir nada. La carga inicial es parte de la
  implementación que estamos cobrando; hacerla a mano no escala ni a tres clientes.
- Checklist de salida en vivo, escrito en `docs/`: verificación de dominio, prueba del
  sitio en celular, alta de usuarios, prueba de un evento de tracking, respaldo inicial.

---

## Paso 8 — Respaldos y soporte

> **Procedimiento escrito, decisiones pendientes.** [operacion.md](operacion.md#3-respaldos-paso-8)
> tiene el respaldo manual, la prueba de restauración y la planilla de soporte. Falta
> verificar qué respalda el hosting y definir la casilla y el WhatsApp de soporte.

Dos promesas del apartado "Incluido" del abono que hoy no tienen respaldo real.

**Respaldos.** Hay que verificar qué hace efectivamente el hosting, y si no alcanza,
implementar un `POST /api/jobs/respaldo` que vuelque la base a object storage con
retención. Y probar una restauración: un respaldo que nunca se restauró no es un respaldo,
es un archivo.

**Soporte.** Los tiempos de respuesta por plan (48 h / 24 h / 8 h hábiles) necesitan como
mínimo un canal definido y un registro de pedidos con su fecha. Puede ser tan simple como
una casilla de correo y una planilla al principio; lo que no puede es no existir, porque
está escrito en un documento que el cliente firma.

---

## Orden sugerido

Los pasos 1, 2 y 3 son un bloque: sin el modelo de datos no hay panel, y sin panel los
límites no se pueden administrar. El 4 depende de los tres. El 5 es independiente y se
puede hacer en cualquier momento. El 6 hay que decidirlo antes de vender un plan Demanda.
El 7 y el 8 son de operación y pueden ir en paralelo.

**Lo mínimo para poder cobrarle a la primera automotora sin quedar mal:** pasos 1, 2, 3 y
5, más la decisión del paso 6. El 4 se puede sostener a mano durante el primer par de
meses, porque con un cliente el vencimiento se recuerda.

## Convenciones

Rigen las mismas del brief: DTOs separados de entidades, FluentValidation, ProblemDetails,
migraciones versionadas, nada de `any` en el frontend, y jobs como endpoints protegidos por
`X-Job-Secret` en vez de `BackgroundService`.

Dos reglas propias de esta fase:

- **Ningún límite ni precio hardcodeado.** Todo sale de la tabla `planes`. Una promoción no
  puede requerir un deploy.
- **Todo lo que bloquea al cliente se prueba con un test**, incluido el caso de borde de la
  fecha exacta de vencimiento. Un error acá no se ve en desarrollo: se ve el día que una
  automotora se queda sin sitio sin corresponder.
