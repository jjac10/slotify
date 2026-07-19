import { test, expect } from '@playwright/test'

/**
 * Upgrade a Premium por el checkout (simulado sin claves de Stripe): el owner pulsa
 * "Mejorar a Premium" en Configuración → Plan, el navegador pasa por la URL de pago y
 * vuelve a Configuración con ?upgraded=1, el banner de éxito y el plan Premium activo.
 * Con claves reales el mismo botón llevaría a Stripe Checkout.
 */

const PASSWORD = 'SecurePass123!'

test('el owner mejora a Premium pasando por el checkout y vuelve con el plan activo', async ({ page }) => {
  const suffix = `${Date.now()}-${Math.floor(Math.random() * 1e6)}`

  await page.goto('/register')
  await page.getByTestId('register-account-type').selectOption('owner')
  await page.getByTestId('register-name').fill('Dueña Premium')
  await page.getByTestId('register-business-name').fill(`Barbería Premium ${suffix}`)
  await page.getByTestId('register-email').fill(`o-${suffix}@s.test`)
  await page.getByTestId('register-password').fill(PASSWORD)
  await page.getByTestId('register-submit').click()
  await expect(page).toHaveURL(/\/panel$/)

  await page.goto('/configuracion')
  await page.getByTestId('section-toggle-plan').click()
  await expect(page.getByTestId('plan-current')).toContainText('Free', { ignoreCase: true })

  // El botón abre el checkout (simulado): redirección de ida y vuelta. La sección
  // Plan vuelve abierta (defaultOpen con ?upgraded=1) mostrando el banner.
  await page.getByTestId('plan-upgrade').click()
  await expect(page).toHaveURL(/\/configuracion\?upgraded=1$/)
  await expect(page.getByTestId('plan-upgraded-banner')).toBeVisible()
  await expect(page.getByTestId('plan-current')).toContainText('Premium', { ignoreCase: true })

  // Volver a Free sigue siendo directo.
  await page.getByTestId('plan-downgrade').click()
  await expect(page.getByTestId('plan-current')).toContainText('Free', { ignoreCase: true })
})
