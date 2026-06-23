import { test, expect } from '@playwright/test'

/**
 * Re-pulsar en el nav la ruta en la que ya estás recarga la página (la remonta):
 * cambiamos a otra pestaña interna y, al re-pulsar el nav, vuelve al estado inicial.
 */

const PASSWORD = 'SecurePass123!'

test('re-pulsar "Agenda" en el nav recarga la página (vuelve a Próximas)', async ({ page }) => {
  const suffix = `${Date.now()}`
  // Registro como owner → su home es el Panel
  await page.goto('/register')
  await page.getByTestId('register-account-type').selectOption('owner')
  await page.getByTestId('register-name').fill('Owner Reload')
  await page.getByTestId('register-email').fill(`owner-reload-${suffix}@s.test`)
  await page.getByTestId('register-password').fill(PASSWORD)
  await page.getByTestId('register-business-name').fill(`Negocio ${suffix}`)
  await page.getByTestId('register-submit').click()
  await expect(page).toHaveURL(/\/panel/)

  // Agenda (sin reservas) → pestaña Próximas activa por defecto
  await page.goto('/agenda')
  await expect(page.getByTestId('agenda-empty')).toContainText('No hay reservas próximas')

  // Cambiar a Pasadas
  await page.getByTestId('agenda-tab-past').click()
  await expect(page.getByTestId('agenda-empty')).toContainText('No hay reservas pasadas')

  // Re-pulsar "Agenda" en el nav lateral → remonta → vuelve a Próximas
  await page.getByTestId('nav-agenda').click()
  await expect(page.getByTestId('agenda-empty')).toContainText('No hay reservas próximas')
})
