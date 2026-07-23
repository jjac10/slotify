import { test, expect } from '@playwright/test'

/**
 * Marca propia del negocio: el owner sube un logo y fija un color de marca en
 * Configuración → Perfil, y ambos se lucen en su ficha pública de Explorar
 * (logo junto al nombre; color en la banda superior si no hay foto).
 */

const PASSWORD = 'SecurePass123!'

// PNG válido de 1×1 px (transparente).
const TINY_PNG = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==',
  'base64',
)

test('el owner pone logo y color de marca y se ven en su ficha pública', async ({ page }) => {
  const suffix = `${Date.now()}-${Math.floor(Math.random() * 1e6)}`
  const businessName = `Barbería Marca ${suffix}`

  await page.goto('/register')
  await page.getByTestId('register-account-type').selectOption('owner')
  await page.getByTestId('register-name').fill('Dueña Marca')
  await page.getByTestId('register-business-name').fill(businessName)
  await page.getByTestId('register-email').fill(`o-${suffix}@s.test`)
  await page.getByTestId('register-password').fill(PASSWORD)
  await page.getByTestId('register-submit').click()
  await expect(page).toHaveURL(/\/panel$/)

  // Configuración → Perfil: subir logo (guarda al elegirlo) y fijar color.
  await page.goto('/configuracion')
  await page.getByTestId('section-toggle-perfil').click()
  await page.getByTestId('profile-logo-file').setInputFiles({
    name: 'logo.png',
    mimeType: 'image/png',
    buffer: TINY_PNG,
  })
  await expect(page.getByTestId('profile-logo-preview')).toBeVisible()

  await page.getByTestId('profile-brand-color').fill('#e91e63')
  await page.getByTestId('profile-save').click()
  await expect(page.getByTestId('profile-saved')).toBeVisible()

  // Ficha pública: el logo aparece junto al nombre y la banda usa el color
  // de marca (el negocio recién creado no tiene foto).
  await page.goto('/explorar')
  await page.getByTestId('explore-search').fill(businessName)
  const item = page.getByTestId('explore-item').first()
  await expect(item).toBeVisible()
  await item.click()

  const modal = page.getByTestId('business-modal')
  await expect(modal).toBeVisible()
  await expect(page.getByTestId('business-modal-logo')).toBeVisible()
  await expect(page.getByTestId('business-modal-band')).toHaveCSS('background-color', 'rgb(233, 30, 99)')
})
