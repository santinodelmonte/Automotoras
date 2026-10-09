import { expect, test } from '@playwright/test'
import { erroresDeConsola } from './ayudas'

/**
 * El recorrido del comprador: portada, listado, ficha y contacto. Corre en escritorio y en
 * celular, que es desde donde entra la mayoría.
 */
test.describe('sitio público', () => {
  test('de la portada a la consulta por WhatsApp, sin errores ni bloqueos de la política de contenido', async ({ page, context }) => {
    const errores = erroresDeConsola(page)

    await page.goto('/t/norte/')
    await expect(page.getByRole('heading', { level: 1 })).toContainText('Automotora Norte')

    await page.getByRole('link', { name: /veh[ií]culos/i }).first().click()
    await expect(page).toHaveURL(/\/t\/norte\/vehiculos/)
    await expect(page.getByRole('heading', { name: 'Vehículos', level: 1 })).toBeVisible()

    await page.locator('a[href*="/t/norte/vehiculos/"]').first().click()
    await expect(page).toHaveURL(/\/t\/norte\/vehiculos\/\d+/)

    // El de la ficha, no el de la cabecera: ese es el contacto general y no se mide.
    const whatsapp = page.getByRole('link', { name: 'Consultar por WhatsApp' }).first()
    await expect(whatsapp).toBeVisible()
    await expect(whatsapp).toHaveAttribute('href', /text=/)

    // El toque en WhatsApp se mide: es la consulta que alimenta los reportes del dueño.
    const evento = page.waitForResponse(
      (r) => r.url().includes('/api/public/events') && r.request().postData()?.includes('ClickWhatsapp') === true,
    )
    const pestana = context.waitForEvent('page')
    await whatsapp.click()
    expect((await evento).status()).toBe(202)
    await (await pestana).close()

    expect(errores.filter((e) => /Content Security Policy|Refused to/i.test(e))).toEqual([])
  })

  test('una dirección que no existe responde 404 y ofrece por dónde seguir', async ({ page }) => {
    const respuesta = await page.goto('/t/norte/esto-no-existe')

    expect(respuesta?.status()).toBe(404)
    await expect(page.getByRole('heading', { name: 'No encontramos esta página' })).toBeVisible()

    await page.getByRole('link', { name: 'Ver vehículos' }).click()
    await expect(page).toHaveURL(/\/t\/norte\/vehiculos$/)
  })

  test('la política de privacidad está enlazada desde el pie', async ({ page }) => {
    await page.goto('/t/norte/')
    await page.getByRole('contentinfo').getByRole('link', { name: 'Privacidad' }).click()

    await expect(page.getByRole('heading', { name: 'Política de privacidad' })).toBeVisible()
    await expect(page.getByText('Ley 18.331')).toBeVisible()
  })

  test('una automotora que no existe lo dice', async ({ page }) => {
    await page.goto('/t/no-existe-esta/')
    await expect(page.getByText('No encontramos esta automotora')).toBeVisible()
  })
})
