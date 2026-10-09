# Operación — alta de clientes, dominios, respaldos y soporte

> Lo que no es código pero que hay que hacer igual para entregar el sistema a un cliente que
> paga. Cubre los pasos 6, 7 y 8 de [fase3-comercializacion.md](fase3-comercializacion.md).
> Lo marcado **[decidir]** necesita una decisión que todavía no está tomada.

---

## 1. Alta de una automotora nueva (paso 7)

La propuesta promete salida en vivo en cuatro semanas. El alta no puede depender de
acordarse de los pasos.

### Semana 1 — alta y datos

- [ ] **Panel → Automotoras → Nueva automotora.** Nombre, slug, plan, dominio si el plan lo
      incluye, colores y contacto si ya los mandaron, y el dueño con una contraseña
      provisoria. La automotora queda con los dos meses bonificados registrados como cobro
      de monto cero.
- [ ] Mandarle al dueño su usuario y la contraseña provisoria por un canal distinto del que
      se usó para el usuario. El sistema le exige cambiarla en el primer ingreso.
- [ ] **Panel → Cobranza:** confirmar que aparece con el plan correcto y vencimiento dentro
      de dos meses.
- [ ] Pedir a la automotora: logo (PNG o SVG con fondo transparente), colores, WhatsApp,
      teléfono, dirección y horarios.

### Semana 2 — identidad y stock

- [ ] Con el dueño, subir el logo y completar lo que falte en **Configuración**. La
      tarjeta *Primeros pasos* del tablero del dueño dice qué falta; cuando desaparece,
      la identidad y el stock están completos.
- [ ] **Carga inicial de stock:** Panel → Cobranza → abrir la automotora → *Carga inicial de
      stock*. Descargar la plantilla, completarla con la automotora, **Validar**, corregir
      lo que marque y recién ahí **Cargar**. Es todo o nada: si una fila falla no se carga
      ninguna.
- [ ] Subir las fotos de cada unidad desde el panel (la carga por CSV no incluye fotos).
- [ ] Si un modelo no está en el catálogo, darlo de alta en **Catálogo** antes de importar.
      Los que la automotora pida desde el formulario del vehículo llegan a **Solicitudes**:
      revisarlas a diario la primera semana.

### Semana 3 — revisión

- [ ] Probar el sitio **en un celular**: home, listado con filtros, ficha, botón de
      WhatsApp con el mensaje armado, botón de llamada.
- [ ] Compartir el link de una ficha por WhatsApp y comprobar que se vea la vista previa.
- [ ] Dar de alta a los vendedores (respetando el tope de usuarios del plan).
- [ ] Capacitación: hasta dos horas, más el instructivo escrito
      ([instructivo-para-automotoras.md](instructivo-para-automotoras.md)). Completar antes
      los datos de soporte marcados **[decidir]**.

### Semana 4 — salida en vivo

- [ ] Dominio propio, si el plan lo incluye (sección 2).
- [ ] **Prueba de tracking:** abrir una ficha desde un navegador sin sesión, tocar WhatsApp,
      y verificar en el tablero del dueño que la vista y la consulta aparecen.
- [ ] Respaldo inicial de la base (sección 3) con la automotora ya cargada.
- [ ] Avisar a la automotora y cobrar el 50 % restante de la implementación.

---

## 2. Dominios propios (paso 6)

**Decisión tomada:** alta manual por cliente desde el panel del hosting. Con los clientes
que contempla la propuesta es viable y no requiere desarrollo. Se pasa a un proxy con TLS
automático (Cloudflare u otro) cuando el alta manual empiece a molestar; el criterio
concreto es **más de cinco dominios propios**, o el primer certificado que se venza sin que
nadie lo note.

Procedimiento por dominio:

1. La automotora registra el dominio, o se registra a costo (adicional de la propuesta).
2. En el DNS del dominio, un registro `A` (y `www` como `CNAME` o `A`) apuntando a las IP
   que figuran en `Deploy:IpsPublicas`.
3. Panel → Automotoras → cargar el dominio en la automotora (el plan tiene que incluirlo).
4. Botón **Verificar**. Hasta que no da *Verificado*, el sitio no responde por ese dominio:
   es lo que impide que alguien cargue el dominio de otra empresa.
5. En el panel del hosting: agregar el dominio como *binding* del sitio y emitir el
   certificado TLS. **[decidir]** quién lo hace y anotar acá los pasos exactos del panel
   de SmarterASP.NET la primera vez.
6. Abrir `https://<dominio>` y `https://www.<dominio>` y verificar el candado.
7. Anotar en la planilla de soporte la fecha de vencimiento del certificado, si el hosting
   no lo renueva solo.

---

## 3. Respaldos (paso 8)

La propuesta promete *respaldos periódicos de la información*. Un respaldo que nunca se
restauró no es un respaldo: es un archivo.

### Lo que hay que verificar una vez, antes del primer cliente

- [ ] **[decidir]** Qué hace el hosting: frecuencia, cuántos días guarda y cómo se pide una
      restauración. Anotarlo acá.
- [ ] Si no hay respaldo automático diario con al menos 14 días de retención, hacer uno
      propio con el procedimiento de abajo, disparado por el mismo cron externo que los jobs.

### Respaldo manual

```bash
mysqldump --single-transaction --routines --triggers --default-character-set=utf8mb4 -h <host> -u <usuario> -p automotora_saas > respaldo-AAAA-MM-DD.sql
```

Comprimirlo y guardarlo **fuera del hosting** (el bucket de R2 en una carpeta privada, o
un disco aparte). Las fotos ya viven en R2 y no hace falta respaldarlas con la base.

### Prueba de restauración — una vez por trimestre

1. Crear una base vacía en un MySQL local (o una MariaDB descartable).
2. `mysql -u root automotora_prueba < respaldo-AAAA-MM-DD.sql`
3. Levantar la API apuntando a esa base y entrar con un usuario real.
4. Anotar acá la fecha y cuánto tardó.

| Fecha | Respaldo restaurado | Tiempo | Quién |
| --- | --- | --- | --- |
| | | | |

---

## 4. Soporte (paso 8)

La propuesta compromete tiempos de respuesta por plan, y está en un documento que el
cliente firma:

| Plan | Canal | Respuesta |
| --- | --- | --- |
| Vidriera | Correo | 48 h |
| Demanda | Correo y WhatsApp | 24 h |
| Full | Prioritario | 8 h hábiles |

Lo mínimo para cumplirlo:

- [ ] **[decidir]** Una casilla de correo de soporte y un número de WhatsApp de soporte,
      separados de los personales.
- [ ] Una planilla de pedidos con: fecha y hora de entrada, automotora, plan, canal, pedido,
      fecha y hora de primera respuesta, fecha de cierre, horas de ajustes consumidas.
- [ ] Revisar la planilla una vez por semana contra los tiempos de la tabla. Las horas de
      ajustes incluidas (Demanda 2 h, Full 5 h) se descuentan de ahí.

---

## 5. Jobs que tiene que disparar el cron externo

Todos con el header `X-Job-Secret`. No hay procesos en segundo plano en el servidor: el
app pool de IIS recicla cuando quiere.

| Job | Frecuencia |
| --- | --- |
| `POST /api/jobs/cotizaciones` | Diaria |
| `POST /api/jobs/precios-de-mercado` | Diaria |
| `POST /api/jobs/avisos-de-vencimiento` | Diaria, a la mañana. Necesita `Correo:*` configurado; la respuesta dice cuántos avisos salieron y cuántos fallaron |
| `POST /api/jobs/limpieza-de-analitica` | Semanal. Borra el detalle de visitas y búsquedas más viejo que `Analitica:MesesDeRetencion` (24 meses; nunca menos de 13) |

---

## 6. Monitoreo

Dos cosas distintas: enterarse de que **algo falla** (un error en un endpoint) y enterarse
de que **todo está caído** (el sitio no responde). Las dos antes que el cliente.

### Errores — Sentry

La API reporta a Sentry las excepciones no manejadas y los logs de nivel `Error`. Sin DSN
configurado no reporta nada, que es lo que pasa en desarrollo y en los tests.

- [ ] Crear una cuenta en [sentry.io](https://sentry.io) (el plan gratuito alcanza) y un
      proyecto de tipo **ASP.NET Core**.
- [ ] Copiar el DSN del proyecto a la configuración del servidor: `Sentry__Dsn` como
      variable de entorno o `"Sentry": { "Dsn": "..." }` en `appsettings.Production.json`.
- [ ] En Sentry, activar la alerta por correo de *issue nuevo*.
- [ ] Verificar: forzar un error (por ejemplo, un job con la base apagada) y confirmar que
      llega el correo.

No viajan datos personales: ni IPs, ni usuarios, ni cuerpos de request
(`SendDefaultPii = false`).

### Caída — chequeo externo

- [ ] Dar de alta un monitor HTTP en un servicio externo (UptimeRobot, Better Stack o
      similar, todos con plan gratuito) contra `https://<dominio-del-saas>/api/health`,
      cada 5 minutos, con aviso por correo y WhatsApp/Telegram.
- [ ] Un monitor más por cada automotora con dominio propio, contra la home de su sitio:
      es lo que se cae si el binding o el certificado del dominio fallan.

`/api/health` responde 200 sin tocar la base: dice que la aplicación levantó, no que la
base esté arriba. Una base caída la avisa Sentry, con el primer request que falle.

---

## 7. Primera puesta en producción

Una sola vez, antes del primer cliente. Las versiones siguientes son solo los pasos 4 a 6.

1. **Base:** crear la base MySQL desde el panel del hosting y aplicar el script de
   migraciones (`dotnet dotnet-ef migrations script --idempotent`, ver el README). Trae el
   catálogo base de marcas y modelos.
2. **Bucket de fotos:** crear el bucket en R2, una clave de API con permiso de escritura
   solo sobre ese bucket, y el dominio público del bucket (https).
3. **Configuración del servidor**, por variables de entorno o en un
   `appsettings.Production.json` que vive solo en el servidor (nunca en el repo):

   | Clave | Qué va |
   | --- | --- |
   | `ASPNETCORE_ENVIRONMENT` | `Production` |
   | `ConnectionStrings__Default` | La del panel del hosting |
   | `Jwt__Secret` | Aleatoria, 32 caracteres o más |
   | `Jobs__Secret` | Aleatoria; la misma va en el header `X-Job-Secret` del cron |
   | `Storage__Provider` | `R2` |
   | `Storage__PublicBaseUrl` | El dominio público del bucket, https y sin barra final |
   | `Storage__Bucket`, `Storage__Endpoint`, `Storage__AccessKeyId`, `Storage__SecretAccessKey` | Los del paso 2 |
   | `Deploy__IpsPublicas__0` | La IP del sitio en el hosting |
   | `Correo__*` | **[decidir]** el servicio de SMTP |
   | `Sentry__Dsn` | Sección 6 |

   Con algo de las primeras siete filas mal, la API no arranca (500.30); el README,
   sección *Publicar*, dice cómo ver qué falta. Las últimas tres solo dejan advertencias.
4. `tools/publicar.ps1` y subir el contenido de `.publish/`.
5. Abrir `https://<dominio-del-saas>/api/health` y entrar al panel con el SuperAdmin
   (ver abajo cómo se crea el primero).
6. Dar de alta los jobs en el cron externo (sección 5) y los monitores (sección 6), y
   disparar cada job una vez a mano para ver que responde 200.

**El primer SuperAdmin.** El seed solo corre en Development. En la base de producción se
crea desde la configuración: poner `PrimerSuperAdmin__Email` y `PrimerSuperAdmin__Password`
(10 caracteres o más, con letras y números), arrancar, entrar al panel —pide cambiar la
contraseña— y **sacar las dos variables**. Solo actúan si la base no tiene ningún
SuperAdmin: olvidadas ahí no crean otro ni pisan la contraseña, pero dejan una advertencia
en el log en cada arranque.
