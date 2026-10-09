#!/usr/bin/env node
// @ts-check

/**
 * Snapshot diario de precios de referencia: consulta MercadoLibre y se lo manda a la API.
 *
 * Corre afuera del servidor web, disparado por el mismo cron que ya dispara los jobs. No
 * está adentro de la API a propósito: el deploy es shared hosting Windows/IIS, y un
 * barrido de precios son cientos de llamadas salientes. Cada una que se cuelgue se lleva
 * un hilo del app pool que atiende a todas las automotoras a la vez, para llenar una tabla
 * que a nadie le urge al segundo.
 *
 * Uso:
 *
 *   API_BASE_URL=https://api.tu-dominio.uy \
 *   JOB_SECRET=... \
 *   node tools/precios-de-mercado.mjs
 *
 * Variables:
 *   API_BASE_URL      Base de la API. Obligatoria.
 *   JOB_SECRET        El mismo valor que Jobs:Secret en la API. Obligatoria.
 *   ML_CLIENT_ID      Client id de la aplicación de MercadoLibre.
 *   ML_CLIENT_SECRET  Su client secret.
 *   ML_TOKEN          Alternativa a los dos anteriores: un access token ya emitido.
 *   ML_SITE           Sitio de MercadoLibre. Por defecto MLU (Uruguay).
 *   ML_CATEGORIA      Categoría de autos y camionetas. Por defecto MLU1744.
 *   DRY_RUN           Con cualquier valor, imprime el lote y no lo manda.
 *
 * Hace falta credencial: la API de búsqueda de MercadoLibre dejó de ser abierta y
 * responde 403 a cualquier consulta sin token. Se saca creando una aplicación en
 * developers.mercadolibre.com; con su client id y su secret, este script pide el token
 * solo. Un ML_TOKEN pegado a mano también sirve, pero vence a las pocas horas y un cron
 * diario lo encuentra vencido siempre: sirve para probar, no para dejarlo andando.
 */

const apiBaseUrl = requerida('API_BASE_URL').replace(/\/+$/, '')
const jobSecret = requerida('JOB_SECRET')

const sitio = process.env.ML_SITE ?? 'MLU'
const categoria = process.env.ML_CATEGORIA ?? 'MLU1744'
const clientId = process.env.ML_CLIENT_ID ?? ''
const clientSecret = process.env.ML_CLIENT_SECRET ?? ''

/** Se resuelve una vez en el arranque y se reusa en todo el barrido. */
let token = process.env.ML_TOKEN ?? ''
const dryRun = Boolean(process.env.DRY_RUN)

/** Avisos mínimos para que la mediana signifique algo. Es el mismo umbral que valida la API. */
const PUBLICACIONES_MINIMAS = 3

/** Techo de avisos por consulta. Más que esto es la cola de resultados que ya no matchea. */
const AVISOS_POR_CONSULTA = 50

/**
 * Pausa entre consultas.
 *
 * MercadoLibre corta por ráfaga, no por volumen: sin esto el barrido entero se cae a los
 * treinta segundos y el snapshot del día queda a medias.
 */
const PAUSA_MS = 250

/** Moneda que se guarda. Los avisos en otra moneda se descartan en vez de convertirse. */
const MONEDA = 'USD'

async function main() {
  if (!token) token = await pedirToken()

  const modelos = await modelosACotizar()

  console.log(`${modelos.length} modelos a cotizar.`)

  const precios = []

  for (const modelo of modelos) {
    for (const anio of modelo.anios) {
      const muestra = await cotizar(modelo, anio)

      if (muestra) precios.push(muestra)

      await esperar(PAUSA_MS)
    }
  }

  if (precios.length === 0) {
    console.log('Ningún modelo juntó publicaciones suficientes. No se manda nada.')
    return
  }

  const lote = {
    fecha: new Date().toISOString().slice(0, 10),
    fuente: 'MercadoLibre',
    precios,
  }

  if (dryRun) {
    console.log(JSON.stringify(lote, null, 2))
    return
  }

  const resultado = await postear('/api/jobs/precios-de-mercado', lote)

  console.log(`Guardados: ${resultado.guardados}. Actualizados: ${resultado.actualizados}.`)
}

/**
 * Pide un access token con el client id y el secret de la aplicación.
 *
 * Es el flujo de client credentials, el que corresponde para un proceso sin usuario
 * detrás. El token dura pocas horas, así que se pide en cada corrida en vez de guardarse:
 * un token cacheado entre ejecuciones diarias siempre llega vencido.
 */
async function pedirToken() {
  if (!clientId || !clientSecret) {
    console.error(
      'Falta la credencial de MercadoLibre. Definí ML_CLIENT_ID y ML_CLIENT_SECRET, o un ' +
        'ML_TOKEN ya emitido: la API de búsqueda responde 403 sin token.',
    )
    process.exit(1)
  }

  const respuesta = await fetch('https://api.mercadolibre.com/oauth/token', {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded', Accept: 'application/json' },
    body: new URLSearchParams({
      grant_type: 'client_credentials',
      client_id: clientId,
      client_secret: clientSecret,
    }),
  })

  if (!respuesta.ok) {
    throw new Error(`MercadoLibre no emitió el token: ${respuesta.status} ${await respuesta.text()}`)
  }

  const datos = await respuesta.json()

  if (!datos.access_token) {
    throw new Error('MercadoLibre respondió sin access_token.')
  }

  return datos.access_token
}

/** Qué modelos y años están publicados hoy. Cotizar el catálogo entero sería malgastar. */
async function modelosACotizar() {
  const respuesta = await fetch(`${apiBaseUrl}/api/jobs/modelos-a-cotizar`, {
    headers: { 'X-Job-Secret': jobSecret },
  })

  if (!respuesta.ok) {
    throw new Error(`La API respondió ${respuesta.status} al pedir los modelos a cotizar.`)
  }

  return await respuesta.json()
}

/**
 * Un modelo y año contra MercadoLibre, reducido a mediana, mínimo y máximo.
 *
 * Devuelve `null` cuando no hay muestra suficiente. Es la decisión importante de todo el
 * script: un precio de referencia sacado de dos avisos es el precio que puso una persona,
 * y mostrado en el panel se lee como si fuera el mercado.
 */
async function cotizar(modelo, anio) {
  const parametros = new URLSearchParams({
    category: categoria,
    q: `${modelo.marca} ${modelo.modelo} ${anio}`,
    limit: String(AVISOS_POR_CONSULTA),
  })

  const url = `https://api.mercadolibre.com/sites/${sitio}/search?${parametros}`
  const cabeceras = token ? { Authorization: `Bearer ${token}` } : undefined

  let respuesta

  try {
    respuesta = await fetch(url, { headers: cabeceras, signal: AbortSignal.timeout(15_000) })
  } catch (problema) {
    // Un modelo que falla no puede llevarse el barrido entero: los otros doscientos
    // precios del día son buenos.
    console.warn(`Falló la consulta de ${modelo.marca} ${modelo.modelo} ${anio}: ${problema}`)
    return null
  }

  if (!respuesta.ok) {
    // El 403 no es un modelo sin avisos: es la credencial. Se avisa distinto porque con
    // el mensaje genérico se lee como que el mercado no tiene ese auto.
    const detalle = respuesta.status === 403 ? ' (¿token vencido o sin permisos?)' : ''

    console.warn(
      `MercadoLibre respondió ${respuesta.status}${detalle} para ${modelo.marca} ${modelo.modelo} ${anio}.`,
    )

    return null
  }

  const datos = await respuesta.json()

  const montos = (datos.results ?? [])
    .filter((aviso) => aviso.currency_id === MONEDA)
    .filter((aviso) => coincideElAnio(aviso, anio))
    .map((aviso) => Number(aviso.price))
    .filter((precio) => Number.isFinite(precio) && precio > 0)
    .sort((a, b) => a - b)

  if (montos.length < PUBLICACIONES_MINIMAS) return null

  return {
    modeloId: modelo.modeloId,
    anio,
    moneda: 'Usd',
    precioMediano: mediana(montos),
    precioMinimo: montos[0],
    precioMaximo: montos[montos.length - 1],
    publicaciones: montos.length,
  }
}

/**
 * El año que declara el aviso, no el que se escribió en la búsqueda.
 *
 * La búsqueda por texto trae de todo: un "Gol 2019" matchea avisos de 2015 que mencionan
 * 2019 en la descripción. Sin este filtro, la referencia de cada año terminaría siendo la
 * misma mediana de todos los años juntos.
 */
function coincideElAnio(aviso, anio) {
  const atributos = aviso.attributes ?? []
  const declarado = atributos.find((a) => a.id === 'VEHICLE_YEAR')

  // Sin el atributo no se puede afirmar el año, y adivinarlo es lo que se quiere evitar.
  if (!declarado) return false

  return Number(declarado.value_name) === anio
}

function mediana(ordenados) {
  const medio = Math.floor(ordenados.length / 2)

  return ordenados.length % 2 === 1
    ? ordenados[medio]
    : Math.round(((ordenados[medio - 1] + ordenados[medio]) / 2) * 100) / 100
}

async function postear(ruta, cuerpo) {
  const respuesta = await fetch(`${apiBaseUrl}${ruta}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'X-Job-Secret': jobSecret },
    body: JSON.stringify(cuerpo),
  })

  if (!respuesta.ok) {
    throw new Error(`La API respondió ${respuesta.status}: ${await respuesta.text()}`)
  }

  return await respuesta.json()
}

function esperar(ms) {
  return new Promise((listo) => setTimeout(listo, ms))
}

function requerida(nombre) {
  const valor = process.env[nombre]

  if (!valor) {
    console.error(`Falta la variable de entorno ${nombre}.`)
    process.exit(1)
  }

  return valor
}

main().catch((problema) => {
  console.error(problema)

  // Con código distinto de cero, para que el cron lo registre como fallido y reintente.
  process.exit(1)
})
