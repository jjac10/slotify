import { test, expect } from '@playwright/test'

/**
 * Borrado de cuenta (derecho de supresión RGPD): un cliente recién registrado
 * elimina su cuenta desde "Mi Slotify" (zona de peligro, confirmando con su
 * contraseña), acaba en la landing con el mensaje de despedida y ya no puede
 * volver a iniciar sesión. Corre contra el backend real (email único por run).
 */

function uniqueEmail(): string {
  const suffix = `${Date.now()}-${Math.floor(Math.random() * 1e6)}`
  return `e2e-del-${suffix}@slotify.test`
}

const PASSWORD = 'SecurePass123!'

test('un cliente borra su cuenta y no puede volver a entrar', async ({ page }) => {
  const email = uniqueEmail()

  // --- Registro (cliente) → Mi Slotify ---
  await page.goto('/register')
  await page.getByTestId('register-account-type').selectOption('customer')
  await page.getByTestId('register-name').fill('Cliente Efímero')
  await page.getByTestId('register-email').fill(email)
  await page.getByTestId('register-password').fill(PASSWORD)
  await page.getByTestId('register-submit').click()
  await expect(page).toHaveURL(/\/inicio$/)

  // --- Zona de peligro → modal de confirmación con contraseña ---
  await page.getByTestId('delete-account-open').click()
  const modal = page.getByTestId('delete-account-modal')
  await expect(modal).toBeVisible()
  await expect(modal).toContainText('irreversible')
  await page.getByTestId('delete-account-password').fill(PASSWORD)
  await page.getByTestId('delete-account-confirm').click()

  // --- Logout automático + landing con mensaje de despedida ---
  await expect(page).toHaveURL(/\/$/)
  await expect(page.getByTestId('account-deleted-banner')).toBeVisible()

  // --- Las credenciales ya no sirven ---
  await page.goto('/login')
  await page.getByTestId('login-email').fill(email)
  await page.getByTestId('login-password').fill(PASSWORD)
  await page.getByTestId('login-submit').click()
  await expect(page.getByTestId('login-error')).toBeVisible()
  await expect(page).toHaveURL(/\/login$/)
})

test('una contraseña incorrecta muestra error y NO borra la cuenta', async ({ page }) => {
  const email = uniqueEmail()

  await page.goto('/register')
  await page.getByTestId('register-account-type').selectOption('customer')
  await page.getByTestId('register-name').fill('Cliente Precavido')
  await page.getByTestId('register-email').fill(email)
  await page.getByTestId('register-password').fill(PASSWORD)
  await page.getByTestId('register-submit').click()
  await expect(page).toHaveURL(/\/inicio$/)

  await page.getByTestId('delete-account-open').click()
  await page.getByTestId('delete-account-password').fill('WrongPass123!')
  await page.getByTestId('delete-account-confirm').click()

  // Error en el modal y la sesión sigue viva (no nos echa a login).
  await expect(page.getByTestId('delete-account-error')).toBeVisible()
  await expect(page).toHaveURL(/\/inicio$/)
})
