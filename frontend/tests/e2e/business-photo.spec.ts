import { test, expect } from '@playwright/test'

/**
 * Subida de la foto del negocio desde Configuración → Perfil (Explorar): el owner
 * elige un fichero, el backend lo guarda en su almacenamiento local y la vista
 * previa muestra la URL servida en /api/uploads/….
 */

const PASSWORD = 'SecurePass123!'

// PNG válido de 1×1 px (transparente).
const TINY_PNG = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==',
  'base64',
)

test('el owner sube la foto del negocio y queda guardada y visible', async ({ page }) => {
  const suffix = `${Date.now()}-${Math.floor(Math.random() * 1e6)}`

  await page.goto('/register')
  await page.getByTestId('register-account-type').selectOption('owner')
  await page.getByTestId('register-name').fill('Dueña Foto')
  await page.getByTestId('register-business-name').fill(`Barbería Foto ${suffix}`)
  await page.getByTestId('register-email').fill(`o-${suffix}@s.test`)
  await page.getByTestId('register-password').fill(PASSWORD)
  await page.getByTestId('register-submit').click()
  await expect(page).toHaveURL(/\/panel$/)

  await page.goto('/configuracion')
  await page.getByTestId('section-toggle-perfil').click()

  // Elegir el fichero sube la foto al momento (sin pasar por "Guardar").
  await page.getByTestId('profile-photo-file').setInputFiles({
    name: 'foto.png',
    mimeType: 'image/png',
    buffer: TINY_PNG,
  })

  // El campo URL pasa a apuntar a la foto servida por el backend…
  await expect(page.getByTestId('profile-photo')).toHaveValue(/\/api\/uploads\/businesses\/.+\.png\?v=/)
  // …y la vista previa la carga de verdad (naturalWidth > 0 = el GET devolvió imagen).
  const preview = page.getByTestId('profile-photo-preview')
  await expect(preview).toBeVisible()
  await expect.poll(async () =>
    preview.evaluate((img: HTMLImageElement) => img.naturalWidth),
  ).toBeGreaterThan(0)

  // Persistida: al recargar sigue ahí.
  await page.reload()
  await page.getByTestId('section-toggle-perfil').click()
  await expect(page.getByTestId('profile-photo')).toHaveValue(/\/api\/uploads\/businesses\/.+\.png\?v=/)
})
