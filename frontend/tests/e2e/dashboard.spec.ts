import { test, expect } from '@playwright/test'

/**
 * El owner entra a su panel y ve el resumen del negocio: contadores de reservas,
 * ingresos del mes y las próximas reservas.
 *
 * Precondición (vía API): owner + servicio + una reserva futura.
 */

const BASE_API = 'http://localhost:5000'
const PASSWORD = 'SecurePass123!'

interface OwnerSetup {
  email: string
  businessId: string
}

async function setupOwnerWithReservation(): Promise<OwnerSetup> {
  const email = `owner-${Date.now()}-${Math.floor(Math.random() * 1e6)}@slotify.test`

  // Registrar owner + negocio
  const ownerRes = await fetch(`${BASE_API}/auth/register-owner`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password: PASSWORD, name: 'Owner Panel', businessName: 'Salón Panel' }),
  })
  if (!ownerRes.ok) throw new Error(`register-owner failed: ${await ownerRes.text()}`)
  const { businessId, accessToken } = await ownerRes.json() as { businessId: string; accessToken: string }

  // Crear servicio (25 €)
  const svcRes = await fetch(`${BASE_API}/businesses/${businessId}/services`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${accessToken}` },
    body: JSON.stringify({ name: 'Corte', description: null, durationMinutes: 30, price: 25, color: null }),
  })
  if (!svcRes.ok) throw new Error(`create-service failed: ${await svcRes.text()}`)
  const service = await svcRes.json() as { id: string }

  // El owner es staff de su propio negocio
  const staffRes = await fetch(`${BASE_API}/businesses/${businessId}/staff`)
  const staff = await staffRes.json() as Array<{ id: string }>

  // Crear una reserva futura (invitado) → cuenta en total + próximas
  const future = new Date()
  future.setDate(future.getDate() + 1)
  future.setUTCHours(10, 0, 0, 0)
  const resvRes = await fetch(`${BASE_API}/reservations`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      businessId,
      serviceId: service.id,
      staffId: staff[0].id,
      startTime: future.toISOString(),
      guestName: 'Juan',
      guestPhone: '+34900111222',
    }),
  })
  if (!resvRes.ok) throw new Error(`create-reservation failed: ${await resvRes.text()}`)

  return { email, businessId }
}

test('el owner ve el resumen de su negocio en el panel', async ({ page }) => {
  const { email } = await setupOwnerWithReservation()

  // Entrar como owner
  await page.goto('/login')
  await page.getByTestId('login-email').fill(email)
  await page.getByTestId('login-password').fill(PASSWORD)
  await page.getByTestId('login-submit').click()
  await expect(page).toHaveURL(/\/mis-reservas$/)

  // El owner ve el enlace al panel y entra
  await page.getByTestId('nav-dashboard').click()
  await expect(page).toHaveURL(/\/panel$/)

  // Métricas visibles con la reserva creada
  await expect(page.getByTestId('dashboard-metrics')).toBeVisible()
  await expect(page.getByTestId('metric-total-reservations')).toContainText('1')

  // La próxima reserva aparece en la lista
  await expect(page.getByTestId('dashboard-upcoming-list')).toBeVisible()
  const upcoming = await page.getByTestId('dashboard-upcoming-item').all()
  expect(upcoming.length).toBeGreaterThan(0)
})
