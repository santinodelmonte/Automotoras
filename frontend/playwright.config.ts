import { defineConfig, devices } from '@playwright/test'
import { BASE } from './e2e/datos.mjs'

/**
 * Tests de punta a punta: la aplicación entera levantada por e2e/servidor.mjs, con una
 * base descartable. Corren con el Edge instalado (sin descargar navegadores); en una
 * máquina sin Edge, E2E_NAVEGADOR=chromium y `npx playwright install chromium`.
 */
export default defineConfig({
  testDir: './e2e',
  // De a uno: comparten la base, y el alta de una automotora no puede pisarse con otra.
  workers: 1,
  fullyParallel: false,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? 'github' : 'list',
  timeout: 30_000,
  use: {
    baseURL: BASE,
    locale: 'es-UY',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    {
      name: 'escritorio',
      use: {
        ...devices['Desktop Chrome'],
        channel: process.env.E2E_NAVEGADOR === 'chromium' ? undefined : 'msedge',
      },
    },
    {
      name: 'celular',
      testMatch: /sitio-publico/,
      use: {
        ...devices['Pixel 7'],
        channel: process.env.E2E_NAVEGADOR === 'chromium' ? undefined : 'msedge',
      },
    },
  ],
  webServer: {
    command: 'node e2e/servidor.mjs',
    url: `${BASE}/api/health`,
    // Compila frontend y API antes de arrancar.
    timeout: 240_000,
    reuseExistingServer: !process.env.CI,
    stdout: 'ignore',
    stderr: 'pipe',
  },
})
