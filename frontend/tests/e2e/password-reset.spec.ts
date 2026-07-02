import { test, expect } from '@playwright/test'

/**
 * Recuperación de contraseña (e2e del flujo público):
 * - Desde login se llega a /recuperar y el formulario muestra SIEMPRE el mensaje
 *   genérico (anti-enumeración), exista o no el email.
 * - /restablecer sin token (o con token inválido) muestra el error con enlace
 *   para pedir otro.
 *
 * El flujo completo con token real se cubre en los tests de integración del
 * backend (el token solo viaja en el "email" simulado, no llega al navegador).
 */

test('desde login se puede pedir la recuperación y se ve el mensaje genérico', async ({ page }) => {
  await page.goto('/login')
  await page.getByTestId('forgot-password-link').click()
  await expect(page).toHaveURL(/\/recuperar$/)

  // Email que no existe: misma respuesta genérica (nada de enumerar usuarios).
  await page.getByTestId('forgot-email').fill(`no-existe-${Date.now()}@slotify.test`)
  await page.getByTestId('forgot-submit').click()

  await expect(page.getByTestId('forgot-success')).toBeVisible()
  await expect(page.getByTestId('forgot-success')).toContainText('Si el email existe')
  // El formulario desaparece: no hay pista de si el email existía.
  await expect(page.getByTestId('forgot-email')).toHaveCount(0)
})

test('restablecer sin token muestra enlace para pedir uno nuevo', async ({ page }) => {
  await page.goto('/restablecer')

  await expect(page.getByTestId('reset-invalid-token')).toBeVisible()
  await page.getByTestId('reset-request-again').click()
  await expect(page).toHaveURL(/\/recuperar$/)
})

test('restablecer con token inválido muestra el error tras enviar', async ({ page }) => {
  await page.goto('/restablecer?token=token-invalido-e2e')

  await page.getByTestId('reset-password').fill('NuevaSecure123!')
  await page.getByTestId('reset-confirm').fill('NuevaSecure123!')
  await page.getByTestId('reset-submit').click()

  await expect(page.getByTestId('reset-invalid-token')).toBeVisible()
})

test('las contraseñas deben coincidir antes de llamar a la API', async ({ page }) => {
  await page.goto('/restablecer?token=cualquiera')

  await page.getByTestId('reset-password').fill('NuevaSecure123!')
  await page.getByTestId('reset-confirm').fill('Distinta123!')
  await page.getByTestId('reset-submit').click()

  await expect(page.getByTestId('reset-error')).toContainText('no coinciden')
})
