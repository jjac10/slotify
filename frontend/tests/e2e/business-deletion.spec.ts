import { test, expect } from '@playwright/test'

/**
 * Borrado del negocio desde Configuración (zona de peligro) con confirmación máxima:
 * el owner debe escribir el nombre EXACTO del negocio y su contraseña. Tras el borrado
 * la cuenta sigue viva pero sin negocio (recarga hacia la home). El botón de confirmar
 * queda deshabilitado hasta que el nombre coincide.
 */

function uniqueEmail(): string {
  const suffix = `${Date.now()}-${Math.floor(Math.random() * 1e6)}`
  return `e2e-bizdel-${suffix}@slotify.test`
}

const PASSWORD = 'SecurePass123!'

async function registerOwner(page: import('@playwright/test').Page, businessName: string) {
  await page.goto('/register')
  await page.getByTestId('register-account-type').selectOption('owner')
  await page.getByTestId('register-name').fill('Dueña Efímera')
  await page.getByTestId('register-business-name').fill(businessName)
  await page.getByTestId('register-email').fill(uniqueEmail())
  await page.getByTestId('register-password').fill(PASSWORD)
  await page.getByTestId('register-submit').click()
  await expect(page).toHaveURL(/\/panel$/)
}

test('el owner elimina su negocio con nombre exacto + contraseña', async ({ page }) => {
  const businessName = `Barbería Fugaz ${Math.floor(Math.random() * 1e6)}`
  await registerOwner(page, businessName)

  await page.goto('/configuracion')
  await page.getByTestId('section-toggle-danger').click() // la sección viene plegada
  await page.getByTestId('delete-business-open').click()
  const modal = page.getByTestId('delete-business-modal')
  await expect(modal).toBeVisible()

  // Con el nombre mal escrito, el botón no se habilita.
  await page.getByTestId('delete-business-name').fill('otro nombre')
  await page.getByTestId('delete-business-password').fill(PASSWORD)
  await expect(page.getByTestId('delete-business-confirm')).toBeDisabled()

  // Con el nombre exacto sí, y el borrado recarga hacia la home sin negocio.
  await page.getByTestId('delete-business-name').fill(businessName)
  await expect(page.getByTestId('delete-business-confirm')).toBeEnabled()
  await page.getByTestId('delete-business-confirm').click()

  await expect(page).toHaveURL(/\/$/)
  // La sesión sigue viva pero ya sin vista de owner (no hay enlace a Configuración).
  await expect(page.getByTestId('delete-business-modal')).toHaveCount(0)
})

test('una contraseña incorrecta muestra error y NO borra el negocio', async ({ page }) => {
  const businessName = `Barbería Robusta ${Math.floor(Math.random() * 1e6)}`
  await registerOwner(page, businessName)

  await page.goto('/configuracion')
  await page.getByTestId('section-toggle-danger').click() // la sección viene plegada
  await page.getByTestId('delete-business-open').click()
  await page.getByTestId('delete-business-name').fill(businessName)
  await page.getByTestId('delete-business-password').fill('Incorrecta123!')
  await page.getByTestId('delete-business-confirm').click()

  await expect(page.getByTestId('delete-business-error')).toBeVisible()
  // El negocio sigue ahí: cerrar el modal y ver su nombre en Configuración.
  await page.getByTestId('delete-business-cancel').click()
  await expect(page.getByTestId('business-name')).toContainText(businessName)
})
