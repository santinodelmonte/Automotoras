import { expect, type Page } from '@playwright/test'

/** Entra al panel por la pantalla de login, como lo haría una persona. */
export async function entrar(page: Page, email: string, password: string) {
  await page.goto('/admin/login')
  await page.getByLabel('Email').fill(email)
  await page.getByLabel('Contraseña').fill(password)
  await page.getByRole('button', { name: 'Entrar' }).click()
  await expect(page).not.toHaveURL(/\/admin\/login/)
}

/**
 * Junta los errores de consola de la página. Una violación de la política de contenido
 * sale por acá: es la forma de enterarse de que un cambio dejó afuera un script o una
 * imagen.
 */
export function erroresDeConsola(page: Page) {
  const errores: string[] = []
  page.on('console', (mensaje) => {
    if (mensaje.type() === 'error') errores.push(mensaje.text())
  })
  page.on('pageerror', (error) => errores.push(error.message))
  return errores
}
