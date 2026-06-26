import { test, expect } from '@playwright/test'

/**
 * Primer hito (TDD, e2e-first): registro → logout → login → ver "mis reservas" vacío.
 * Corre contra el backend real, así que cada ejecución usa un email único.
 */

function uniqueEmail(): string {
  const suffix = `${Date.now()}-${Math.floor(Math.random() * 1e6)}`
  return `e2e-${suffix}@slotify.test`
}

const PASSWORD = 'SecurePass123!'

test('un cliente se registra, vuelve a entrar y ve su inicio (Mi Slotify)', async ({ page }) => {
  const email = uniqueEmail()

  // --- Registro (cliente) ---
  await page.goto('/register')
  await page.getByTestId('register-account-type').selectOption('customer')
  await page.getByTestId('register-name').fill('Cliente E2E')
  await page.getByTestId('register-email').fill(email)
  await page.getByTestId('register-password').fill(PASSWORD)
  await page.getByTestId('register-submit').click()

  // El registro autentica y lleva al home del cliente (Mi Slotify).
  await expect(page).toHaveURL(/\/inicio$/)
  await expect(page.getByTestId('current-user')).toHaveText(email)

  // --- Logout (desde el menú de perfil arriba a la derecha) ---
  await page.getByTestId('profile-button').click()
  await page.getByTestId('logout').click()
  await expect(page).toHaveURL(/\/login$/)

  // --- Login con las mismas credenciales → de nuevo en Mi Slotify ---
  await page.getByTestId('login-email').fill(email)
  await page.getByTestId('login-password').fill(PASSWORD)
  await page.getByTestId('login-submit').click()
  await expect(page).toHaveURL(/\/inicio$/)
})

test('login con credenciales inválidas muestra error', async ({ page }) => {
  await page.goto('/login')
  await page.getByTestId('login-email').fill('no-existe@slotify.test')
  await page.getByTestId('login-password').fill('WrongPass123!')
  await page.getByTestId('login-submit').click()

  await expect(page.getByTestId('login-error')).toBeVisible()
  await expect(page).toHaveURL(/\/login$/)
})
