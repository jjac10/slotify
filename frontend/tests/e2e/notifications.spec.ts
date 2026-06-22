import { test, expect } from '@playwright/test'

/**
 * El owner configura los avisos a clientes (canales + recordatorio) desde Configuración
 * y los cambios persisten. Corre contra el stack real.
 */

function unique(): string {
  return `${Date.now()}-${Math.floor(Math.random() * 1e6)}`
}

const PASSWORD = 'SecurePass123!'

test('el owner configura los avisos y se guardan', async ({ page }) => {
  const suffix = unique()
  const email = `owner-${suffix}@slotify.test`

  await page.goto('/register')
  await page.getByTestId('register-account-type').selectOption('owner')
  await page.getByTestId('register-name').fill('Owner E2E')
  await page.getByTestId('register-email').fill(email)
  await page.getByTestId('register-password').fill(PASSWORD)
  await page.getByTestId('register-business-name').fill(`Barberia ${suffix}`)
  await page.getByTestId('register-submit').click()
  await expect(page).toHaveURL(/\/panel/)

  await page.goto('/configuracion')
  await expect(page).toHaveURL(/\/configuracion/)

  // Desplegar "Avisos a clientes" (las secciones están plegadas).
  await page.getByTestId('section-toggle-notificaciones').click()
  await expect(page.getByTestId('notifications-form')).toBeVisible()

  // Por defecto: email activado, WhatsApp desactivado, recordatorio 24h.
  await expect(page.getByTestId('notify-email')).toBeChecked()
  await expect(page.getByTestId('notify-whatsapp')).not.toBeChecked()

  // Activar WhatsApp y cambiar el recordatorio, guardar.
  await page.getByTestId('notify-whatsapp').check()
  await page.getByTestId('reminder-hours').fill('48')
  await page.getByTestId('notifications-save').click()

  // Recargar: los cambios persisten (round-trip con el backend).
  await page.goto('/configuracion')
  await page.getByTestId('section-toggle-notificaciones').click()
  await expect(page.getByTestId('notify-whatsapp')).toBeChecked()
  await expect(page.getByTestId('reminder-hours')).toHaveValue('48')
})
