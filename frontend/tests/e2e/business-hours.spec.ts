import { test, expect } from '@playwright/test'

/**
 * El owner configura el horario semanal de su negocio desde la UI y lo guarda.
 * El editor prefija L–V 09–17, así que basta con guardar para habilitar reservas.
 */

function unique(): string {
  return `${Date.now()}-${Math.floor(Math.random() * 1e6)}`
}

const PASSWORD = 'SecurePass123!'

test('el owner configura y guarda el horario del negocio', async ({ page }) => {
  const email = `owner-${unique()}@slotify.test`

  // Registro como propietario
  await page.goto('/register')
  await page.getByTestId('register-account-type').selectOption('owner')
  await page.getByTestId('register-name').fill('Owner Horario')
  await page.getByTestId('register-email').fill(email)
  await page.getByTestId('register-password').fill(PASSWORD)
  await page.getByTestId('register-business-name').fill(`Negocio ${unique()}`)
  await page.getByTestId('register-submit').click()

  // Ir a Horario
  await page.getByTestId('nav-hours').click()
  await expect(page).toHaveURL(/\/horario$/)
  await expect(page.getByTestId('hours-form')).toBeVisible()

  // El lunes viene prefijado como abierto (09:00–17:00)
  await expect(page.getByTestId('hours-day-1-opening')).toHaveValue('09:00')

  // Cerrar el sábado y guardar
  await page.getByTestId('hours-day-6-open-toggle').uncheck()
  await page.getByTestId('hours-save').click()

  await expect(page.getByTestId('hours-saved')).toBeVisible()
})
