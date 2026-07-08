import { test, expect } from '@playwright/test'

/**
 * Formulario público de contacto/soporte (/contacto): envía un mensaje al
 * dueño de la plataforma vía POST /support/contact (204; envío simulado en
 * el backend). El camino feliz necesita el backend levantado.
 */

test('un visitante envía un mensaje de soporte y ve la confirmación', async ({ page }) => {
  await page.goto('/contacto')

  await page.getByTestId('contact-name').fill('Vecina del Pueblo')
  await page.getByTestId('contact-email').fill('vecina@example.com')
  await page.getByTestId('contact-message').fill('¡Me encanta Slotify! ¿Podríais añadir mi peluquería?')
  await page.getByTestId('contact-submit').click()

  await expect(page.getByTestId('contact-success')).toBeVisible()
  await expect(page.getByTestId('contact-success')).toContainText('Mensaje enviado')

  // "Enviar otro mensaje" vuelve al formulario limpio
  await page.getByRole('button', { name: 'Enviar otro mensaje' }).click()
  await expect(page.getByTestId('contact-message')).toHaveValue('')
})

test('el botón de enviar está deshabilitado si faltan campos y el email inválido muestra error', async ({ page }) => {
  await page.goto('/contacto')

  // Vacío: no se puede enviar (no llega a llamar a la API)
  await expect(page.getByTestId('contact-submit')).toBeDisabled()

  // Email sin formato válido: el backend responde 400 con el detalle en español
  await page.getByTestId('contact-name').fill('Vecina del Pueblo')
  await page.getByTestId('contact-email').fill('esto-no-es-un-email')
  await page.getByTestId('contact-message').fill('Hola')
  await page.getByTestId('contact-submit').click()

  await expect(page.getByTestId('contact-error')).toBeVisible()
  await expect(page.getByTestId('contact-success')).not.toBeVisible()
})

test('el footer de la landing enlaza a la página de contacto', async ({ page }) => {
  await page.goto('/')

  await page.getByRole('contentinfo').getByRole('link', { name: 'Contacto' }).click()
  await expect(page).toHaveURL(/\/contacto$/)
  await expect(page.getByRole('heading', { name: 'Contacto y soporte' })).toBeVisible()
})
