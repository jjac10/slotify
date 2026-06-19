import { test, expect } from '@playwright/test'

/**
 * Equipo (Team) — sección de /configuracion (solo owner).
 *
 * 1. Premium: el owner añade un empleado y luego lo da de baja.
 * 2. Free: añadir empleado está bloqueado y se muestra el aviso de Premium.
 *
 * Setup vía API (registro owner + promoción a Premium), login por UI —
 * igual que dashboard.spec.ts para evitar la carrera tras el registro.
 */

const BASE_API = 'http://localhost:5000'
const PASSWORD = 'SecurePass123!'

interface OwnerSetup {
  email: string
  businessId: string
  accessToken: string
}

function uniqueEmail(): string {
  return `owner-${Date.now()}-${Math.floor(Math.random() * 1e6)}@slotify.test`
}

async function registerOwner(): Promise<OwnerSetup> {
  const email = uniqueEmail()
  const res = await fetch(`${BASE_API}/auth/register-owner`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password: PASSWORD, name: 'Owner Equipo', businessName: 'Salón Equipo' }),
  })
  if (!res.ok) throw new Error(`register-owner failed: ${await res.text()}`)
  const { businessId, accessToken } = await res.json() as { businessId: string; accessToken: string }
  return { email, businessId, accessToken }
}

async function promoteToPremium(setup: OwnerSetup): Promise<void> {
  const res = await fetch(`${BASE_API}/businesses/${setup.businessId}/plan`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${setup.accessToken}` },
    body: JSON.stringify({ code: 'premium' }),
  })
  if (!res.ok) throw new Error(`promote-to-premium failed: ${await res.text()}`)
}

async function loginAndOpenTeam(page: import('@playwright/test').Page, email: string): Promise<void> {
  await page.goto('/login')
  await page.getByTestId('login-email').fill(email)
  await page.getByTestId('login-password').fill(PASSWORD)
  await page.getByTestId('login-submit').click()
  // El home del owner es el Panel; esperar a estar autenticado antes de ir a /configuracion.
  await expect(page).toHaveURL(/\/panel$/)

  await page.goto('/configuracion')
  await expect(page).toHaveURL(/\/configuracion/)
  await expect(page.getByTestId('staff-list')).toBeVisible()
}

test('Premium: el owner añade un empleado y luego lo da de baja', async ({ page }) => {
  const setup = await registerOwner()
  await promoteToPremium(setup)
  await loginAndOpenTeam(page, setup.email)

  // El owner ya aparece en la lista (no tiene botón de baja).
  await expect(page.getByTestId('staff-item')).toHaveCount(1)

  // Añadir empleado
  await page.getByTestId('staff-name-input').fill('Ana García')
  await page.getByTestId('create-staff-submit').click()

  // Aparece en la lista
  const anaItem = page.getByTestId('staff-item').filter({ hasText: 'Ana García' })
  await expect(anaItem).toBeVisible()
  await expect(page.getByTestId('staff-item')).toHaveCount(2)

  // Dar de baja (aceptar el window.confirm nativo)
  page.on('dialog', (d) => d.accept())
  await anaItem.getByTestId('staff-remove').click()

  // Desaparece
  await expect(page.getByTestId('staff-item').filter({ hasText: 'Ana García' })).toHaveCount(0)
  await expect(page.getByTestId('staff-item')).toHaveCount(1)
})

test('Free: añadir empleado muestra el aviso de Premium', async ({ page }) => {
  const setup = await registerOwner()
  // Sin promoción → sigue en plan Free (solo permite al owner).
  await loginAndOpenTeam(page, setup.email)

  await page.getByTestId('staff-name-input').fill('Beatriz López')
  await page.getByTestId('create-staff-submit').click()

  // El backend devuelve 409 limit_reached → se muestra el aviso de Premium.
  await expect(page.getByTestId('staff-premium-required')).toBeVisible()

  // El empleado NO se añade.
  await expect(page.getByTestId('staff-item').filter({ hasText: 'Beatriz López' })).toHaveCount(0)
})
