# Instructivo del panel

> Guía para el dueño y los vendedores de cada automotora. Es el "instructivo escrito" que
> acompaña la capacitación ([operacion.md](operacion.md), semana 3). Está escrita para
> mandársela tal cual al cliente: si cambia una pantalla, esto cambia con ella.

---

## 1. Entrar

- El panel está en **`/admin`** de la dirección de tu sitio (por ejemplo
  `https://tuautomotora.com.uy/admin`).
- La primera vez entrás con la contraseña provisoria que te mandamos. El sistema te pide
  cambiarla antes de dejarte hacer cualquier otra cosa.
- **¿Te olvidaste la contraseña?** Si sos vendedor, el dueño te pone una provisoria nueva
  desde **Usuarios**. Si sos el dueño, escribinos a soporte.

## 2. Primeros pasos

Al entrar, el **Tablero** muestra la tarjeta *Primeros pasos* con lo que le falta a tu
sitio. Cada paso se marca solo cuando el dato está cargado, y la tarjeta desaparece cuando
está todo:

1. **Logo** — en *Configuración*. PNG o SVG con fondo transparente queda mejor.
2. **Color de la marca** — en *Configuración*. Botones y acentos del sitio salen de ahí; si
   el color es claro, el sistema pone el texto oscuro solo para que se lea.
3. **WhatsApp** — en *Configuración*. Es por donde llegan casi todas las consultas, con el
   auto ya escrito en el mensaje.
4. **Primer vehículo publicado.**
5. **Fotos en todo lo publicado** — una unidad sin foto casi no recibe consultas.
6. **Vendedores** — cada uno con su usuario.

Con el botón *Ver mi sitio* lo abrís como lo ve un comprador.

## 3. Cargar el stock

### De a uno

**Vehículos → Cargar vehículo.**

- Marca y modelo salen del catálogo, para que las búsquedas y los reportes coincidan.
  **¿No está el modelo?** Tocá *¿No está el modelo? Pedilo* debajo del selector, escribí el
  nombre y la carrocería, y lo agregamos. Aparece en la lista cuando se aprueba.
- **Precio y moneda:** en dólares o en pesos, como lo publiques.
- **Precio de costo:** opcional. Solo lo ven los dueños; nunca sale en el sitio.
- **Fecha de publicación:** si la dejás vacía se toma la de hoy. Si la unidad ya estaba a
  la venta, poné la fecha real: cuenta para los días en góndola.
- **Destacar en la home:** aparece primero en la portada del sitio, con etiqueta.

Después de guardar aparece la **galería de fotos**: JPG, PNG o WebP, de hasta 5 MB (se
achican solas antes de subir). La primera es la portada; las podés reordenar.

### Todos juntos, con una planilla (solo el dueño)

**Vehículos → Importar planilla** (o *Mi plan → Cargar stock desde una planilla*).

1. Descargá la plantilla y completala, una fila por unidad.
2. Subila y tocá **Validar**: te marca fila por fila lo que esté mal, sin cargar nada.
3. Corregí y volvé a validar. Cuando no hay errores, **Cargar**.

Es todo o nada: si una fila falla no se carga ninguna, para que no queden unidades a
medias. Las fotos se suben después, desde cada vehículo.

## 4. Estados de una unidad

| Estado | En el sitio | Ocupa lugar del plan |
| --- | --- | --- |
| **Disponible** | Se ve | Sí |
| **Reservado** | No se ve | Sí |
| **Pausado** | No se ve | No |
| **Vendido** | No se ve | No |

Marcar como **Vendido** en vez de borrar: la venta alimenta los reportes (días hasta
vender, qué se vende rápido). El link de una unidad vendida que alguien haya compartido
sigue andando y ofrece el resto del stock.

## 5. Usuarios (solo el dueño)

**Usuarios → Nuevo usuario.** Cada vendedor con su usuario, nunca compartido.

- El **vendedor** carga y actualiza el stock. No ve precios de costo, reportes ni el plan.
- El **dueño** ve todo.
- Para dar de baja a alguien, desactivalo: pierde el acceso en minutos, aunque tenga el
  panel abierto.

La cantidad de usuarios depende del plan.

## 6. Tablero y reportes (solo el dueño)

El **Tablero** resume los últimos 30 días: vistas de fichas, consultas (WhatsApp y
teléfono), días en góndola promedio y búsquedas sin resultado.

En **Demanda**:

- **Unidad por unidad** — cada vehículo publicado con su lectura:
  - *Saludable*: se mira y se consulta en la proporción esperable.
  - *Precio alto*: mucha vista y poca consulta. El aviso interesa y el precio frena.
  - *Sin visibilidad*: casi no se mira. El problema no es el precio, es que no llega nadie.
  - *Estancado*: lleva demasiado en góndola sin una sola consulta.
  - *Sin datos*: todavía no hay tráfico suficiente para decir nada.
- **Búsquedas sin resultado** — lo que buscaron en tu sitio y no encontraron. Es la señal
  más directa de qué stock te están pidiendo.
- **Qué conviene comprar** — sugerencias armadas con esas búsquedas.
- **Contra el resto del mercado** (plan Full) — tus días en góndola y tus consultas
  comparados con el promedio anónimo de las demás automotoras.

Los datos se acumulan con el tráfico: las primeras semanas van a estar casi vacíos.

## 7. Plan, vencimiento y tus datos (solo el dueño)

En **Mi plan** ves qué incluye tu plan, cuánto usás de cada tope y hasta cuándo está pago.

- Unos días antes del vencimiento aparece un aviso arriba del panel, y te llega por correo.
- Si vence, el sitio sigue publicado unos días más. Pasado ese plazo queda **en
  mantenimiento** hasta regularizar. Tus datos no se tocan.
- **Descargar todos mis datos** te da un ZIP con el stock completo (incluido lo vendido),
  la dirección de cada foto y la historia de visitas, consultas y búsquedas, en planillas
  que se abren con Excel. Los datos son tuyos.

## 8. Soporte

Escribinos a **[decidir: casilla de soporte]** o por WhatsApp al **[decidir: número]**,
con el nombre de la automotora y, si es un problema, una captura de pantalla. Los tiempos
de respuesta dependen del plan.
