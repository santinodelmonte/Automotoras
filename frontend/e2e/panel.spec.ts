import { expect, test } from '@playwright/test'
import { entrar } from './ayudas'
import { PASSWORD_DEL_SEED, USUARIOS } from './datos.mjs'

/**
 * El alta de un cliente de punta a punta: el SuperAdmin crea la automotora, el dueño entra
 * con la contraseña provisoria, la cambia, ve sus primeros pasos y publica su primer auto,
 * que aparece en el sitio.
 */
test('alta de una automotora nueva hasta su primer vehículo publicado', async ({ page, browser }) => {
  const slug = `e2e-${Date.now()}`
  const email = `owner@${slug}.uy`
  const provisoria = 'Provisoria-e2e-1'
  const definitiva = 'Definitiva-e2e-2'

  await entrar(page, USUARIOS.superAdmin, PASSWORD_DEL_SEED)
  await expect(page).toHaveURL(/\/admin\/automotoras/)
  await page.getByLabel('Nombre', { exact: true }).fill(`Automotora ${slug}`)
  await page.getByLabel('Slug').fill(slug)
  await page.getByLabel('Nombre del dueño').fill('Dueño de prueba')
  await page.getByLabel('Email del dueño').fill(email)
  await page.getByLabel('Contraseña provisoria del dueño').fill(provisoria)
  await page.getByRole('button', { name: 'Crear automotora' }).click()
  await expect(page.getByText(`/t/${slug}`)).toBeVisible()

  const dueno = await (await browser.newContext()).newPage()
  await entrar(dueno, email, provisoria)
  await dueno.getByLabel('Contraseña provisoria').fill(provisoria)
  await dueno.getByLabel('Contraseña nueva').fill(definitiva)
  await dueno.getByLabel('Repetila').fill(definitiva)
  await dueno.getByRole('button', { name: 'Guardar y entrar' }).click()

  await expect(dueno.getByRole('heading', { name: 'Primeros pasos' })).toBeVisible()
  await expect(dueno.getByRole('link', { name: /Subí tu logo/ })).toBeVisible()

  await dueno.getByRole('link', { name: 'Vehículos' }).first().click()
  await expect(dueno.getByText('Todavía no cargaste ningún vehículo')).toBeVisible()

  await dueno.getByRole('link', { name: 'Cargar vehículo' }).first().click()
  await dueno.getByLabel('Marca').selectOption({ index: 1 })
  await dueno.getByLabel('Modelo').selectOption({ index: 1 })
  await dueno.getByLabel('Kilometraje').fill('50000')
  // La etiqueta lleva el símbolo de la moneda adentro: "Precio US$".
  await dueno.getByLabel(/^Precio(?! de costo)/).fill('15000')
  await dueno.getByRole('button', { name: 'Crear y cargar fotos' }).click()
  await expect(dueno).toHaveURL(/\/admin\/vehiculos\/\d+$/)

  await dueno.goto(`/t/${slug}/vehiculos`)
  await expect(dueno.locator(`a[href*="/t/${slug}/vehiculos/"]`)).toHaveCount(1)
})

test('el vendedor no ve la demanda ni entra a ella por la URL', async ({ page }) => {
  await entrar(page, USUARIOS.vendedorNorte, PASSWORD_DEL_SEED)
  await expect(page).toHaveURL(/\/admin\/vehiculos/)
  await expect(page.getByRole('link', { name: 'Demanda' })).toHaveCount(0)

  await page.goto('/admin/reportes')
  await expect(page).toHaveURL(/\/admin\/vehiculos/)
})
