import { test, expect } from '@playwright/test'

/**
 * Modo "solo calendario": el owner lo activa desde Configuración y su negocio deja
 * de aparecer en Explorar (no acepta reservas online). Corre contra el stack real.
 */

function unique(): string {
  return `${Date.now()}-${Math.floor(Math.random() * 1e6)}`
}

const PASSWORD = 'SecurePass123!'

test('el owner activa "solo calendario" y su negocio desaparece de Explorar', async ({ page }) => {
  const suffix = unique()
  const email = `owner-${suffix}@slotify.test`
  const businessName = `Barberia ${suffix}`

  // Registro como propietario → autenticado como owner.
  await page.goto('/register')
  await page.getByTestId('register-account-type').selectOption('owner')
  await page.getByTestId('register-name').fill('Owner E2E')
  await page.getByTestId('register-email').fill(email)
  await page.getByTestId('register-password').fill(PASSWORD)
  await page.getByTestId('register-business-name').fill(businessName)
  await page.getByTestId('register-submit').click()
  await expect(page).toHaveURL(/\/panel/)

  // Por defecto (online) el negocio aparece en Explorar.
  await page.goto('/explorar')
  await page.getByTestId('explore-search').fill(businessName)
  await expect(page.getByTestId('explore-item').filter({ hasText: businessName })).toBeVisible()

  // El owner activa "solo calendario" en Configuración.
  await page.goto('/configuracion')
  await expect(page).toHaveURL(/\/configuracion/)
  await page.getByTestId('section-toggle-modo-reservas').click()
  await page.getByTestId('booking-mode-calendar-only').click()
  await expect(page.getByTestId('booking-mode-calendar-only-active')).toBeVisible()

  // Ya no aparece en Explorar.
  await page.goto('/explorar')
  await page.getByTestId('explore-search').fill(businessName)
  await expect(page.getByTestId('explore-item').filter({ hasText: businessName })).toHaveCount(0)
})
