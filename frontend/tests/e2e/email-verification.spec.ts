import { test, expect } from '@playwright/test'

/**
 * Verificación de email NO bloqueante:
 * - Al registrarse aparece un aviso discreto "Verifica tu email" con reenvío y
 *   descarte; la app sigue siendo usable sin verificar.
 * - /verificar-email con token inválido muestra el error.
 *
 * El flujo completo con token real se cubre en los tests de integración del
 * backend (el token solo viaja en el "email" simulado, no llega al navegador).
 */

function uniqueEmail(): string {
  const suffix = `${Date.now()}-${Math.floor(Math.random() * 1e6)}`
  return `e2e-verify-${suffix}@slotify.test`
}

const PASSWORD = 'SecurePass123!'

test('tras registrarse aparece el aviso, permite reenviar y se puede descartar', async ({ page }) => {
  const email = uniqueEmail()

  await page.goto('/register')
  await page.getByTestId('register-account-type').selectOption('customer')
  await page.getByTestId('register-name').fill('Cliente Verificación E2E')
  await page.getByTestId('register-email').fill(email)
  await page.getByTestId('register-password').fill(PASSWORD)
  await page.getByTestId('register-submit').click()

  // El registro autentica y lleva al inicio; el aviso (no bloqueante) está visible.
  await expect(page).toHaveURL(/\/inicio$/)
  await expect(page.getByTestId('verify-email-banner')).toBeVisible()
  await expect(page.getByTestId('verify-email-banner')).toContainText('Verifica tu email')

  // Reenviar el enlace muestra la confirmación.
  await page.getByTestId('verify-email-resend').click()
  await expect(page.getByTestId('verify-email-sent')).toBeVisible()

  // Descartar: el aviso desaparece y no vuelve al navegar.
  await page.getByTestId('verify-email-dismiss').click()
  await expect(page.getByTestId('verify-email-banner')).toHaveCount(0)
  await page.getByTestId('nav-my-reservations').click()
  await expect(page).toHaveURL(/\/mis-reservas$/)
  await expect(page.getByTestId('verify-email-banner')).toHaveCount(0)
})

test('la página de verificación con token inválido muestra el error', async ({ page }) => {
  await page.goto('/verificar-email?token=token-invalido-e2e')

  await expect(page.getByTestId('verify-email-error')).toBeVisible()
  await expect(page.getByTestId('verify-email-error')).toContainText('no es válido o ha caducado')
})

test('la página de verificación sin token muestra el error sin llamar a la API', async ({ page }) => {
  await page.goto('/verificar-email')

  await expect(page.getByTestId('verify-email-error')).toBeVisible()
})
