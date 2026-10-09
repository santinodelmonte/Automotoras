// Levanta la aplicación entera para los tests de punta a punta, como en producción: la API
// sirve el frontend compilado desde su wwwroot, en un solo origen y con los headers de
// seguridad reales. La base es SQLite y descartable: se borra, se crea del modelo y se
// siembra en cada corrida, así cada test arranca de los mismos datos.
//
// Lo arranca Playwright (webServer en playwright.config.ts). A mano:
//   node e2e/servidor.mjs
//
// Todo lo que escribe va a .e2e/ en la raíz del repo, ignorado por git. No lee ni toca la
// configuración local de nadie: lo que importa se pisa por variables de entorno.

import { spawn, spawnSync } from 'node:child_process'
import { mkdirSync, rmSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { PASSWORD_DEL_SEED, PUERTO } from './datos.mjs'

const frontend = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const raiz = path.resolve(frontend, '..')
const trabajo = path.join(raiz, '.e2e')
const wwwroot = path.join(trabajo, 'wwwroot')
const artefactos = path.join(trabajo, 'artifacts')
const api = path.join(raiz, 'backend', 'Api')

function correr(comando, argumentos, opciones = {}) {
  const resultado = spawnSync(comando, argumentos, { stdio: 'inherit', shell: true, ...opciones })
  if (resultado.status !== 0) {
    throw new Error(`Falló: ${comando} ${argumentos.join(' ')}`)
  }
}

rmSync(path.join(trabajo, 'e2e.db'), { force: true })
rmSync(path.join(trabajo, 'uploads'), { recursive: true, force: true })
mkdirSync(trabajo, { recursive: true })

// Mismo origen, como en producción: sin VITE_API_BASE_URL el bundle llama a /api.
correr('npx', ['vite', 'build', '--outDir', JSON.stringify(wwwroot), '--emptyOutDir'], {
  cwd: frontend,
  env: { ...process.env, VITE_API_BASE_URL: '' },
})

// En una carpeta propia: la de bin/ puede estar tomada por una API de desarrollo corriendo.
correr('dotnet', ['build', JSON.stringify(api), '--artifacts-path', JSON.stringify(artefactos), '--nologo', '-v', 'q'])

const servidor = spawn(
  'dotnet',
  [
    path.join(artefactos, 'bin', 'Api', 'debug', 'AutomotoraSaaS.Api.dll'),
    '--urls',
    `http://localhost:${PUERTO}`,
    '--webroot',
    wwwroot,
    '--contentRoot',
    api,
  ],
  {
    stdio: 'inherit',
    env: {
      ...process.env,
      ASPNETCORE_ENVIRONMENT: 'Development',
      Database__Proveedor: 'Sqlite',
      ConnectionStrings__Default: `Data Source=${path.join(trabajo, 'e2e.db')}`,
      Seed__Password: PASSWORD_DEL_SEED,
      Jwt__Secret: 'secreto-de-los-tests-de-punta-a-punta-de-sobra-32',
      Storage__Provider: 'Local',
      Storage__LocalRootPath: path.join(trabajo, 'uploads'),
      Storage__PublicBaseUrl: `http://localhost:${PUERTO}/uploads`,
      // Todos los tests entran desde la misma IP: con el tope real se frenarían solos.
      Seguridad__LoginsPorMinutoPorIp: '10000',
      // Sin reporte a Sentry ni correo real aunque la configuración local los tenga.
      Sentry__Dsn: '',
      Correo__Host: '',
    },
  },
)

for (const senal of ['SIGINT', 'SIGTERM']) {
  process.on(senal, () => {
    servidor.kill()
    process.exit(0)
  })
}

servidor.on('exit', (codigo) => process.exit(codigo ?? 0))
