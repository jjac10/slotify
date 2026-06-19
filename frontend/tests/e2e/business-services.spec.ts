import { test, expect } from '@playwright/test'

/**
 * El owner ve su negocio (nombre + id) y crea un servicio desde la UI;
 * el servicio aparece en la lista. Corre contra el stack real.
 */

function unique(): string {
  return `${Date.now()}-${Math.floor(Math.random() * 1e6)}`
}

const PASSWORD = 'SecurePass123!'

test('el owner ve su negocio y crea un servicio', async ({ page }) => {
  const suffix = unique()
  const email = `owner-${suffix}@slotify.test`
  const businessName = `Barbería ${suffix}`

  // Registro como propietario → queda autenticado como owner
  await page.goto('/register')
  await page.getByTestId('register-account-type').selectOption('owner')
  await page.getByTestId('register-name').fill('Owner E2E')
  await page.getByTestId('register-email').fill(email)
  await page.getByTestId('register-password').fill(PASSWORD)
  await page.getByTestId('register-business-name').fill(businessName)
  await page.getByTestId('register-submit').click()

  // El owner navega a Configuración
  await page.goto('/configuracion')
  await expect(page).toHaveURL(/\/configuracion/)

  // Ve el nombre de su negocio + lista de servicios vacía
  await expect(page.getByTestId('business-name')).toHaveText(businessName)
  await expect(page.getByTestId('services-empty')).toBeVisible()

  // Crea un servicio
  await page.getByTestId('service-name').fill('Corte de cabello')
  await page.getByTestId('service-duration').fill('30')
  await page.getByTestId('service-price').fill('25')
  await page.getByTestId('create-service-submit').click()

  // Aparece en la lista
  await expect(page.getByTestId('services-list')).toBeVisible()
  await expect(page.getByTestId('service-item').filter({ hasText: 'Corte de cabello' })).toBeVisible()
})
