import { test, expect } from '@playwright/test'

/**
 * Al registrarte como cliente con tu teléfono (+34), las reservas que hiciste antes
 * como invitado con ese mismo número se vinculan a tu cuenta y aparecen en
 * "Mis reservas". Verifica el +34 del registro + el sync invitado→usuario.
 */

const API = 'http://localhost:5000'
const PASSWORD = 'SecurePass123!'

function nextWeekday(offset = 1): string {
  const d = new Date()
  d.setDate(d.getDate() + offset)
  while (d.getDay() === 0 || d.getDay() === 6) d.setDate(d.getDate() + 1)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

test('registrarse con +34 vincula las reservas previas de invitado', async ({ page }) => {
  const localPhone = `6${String(Date.now()).slice(-8)}` // 9 dígitos únicos
  const fullPhone = `+34${localPhone}`

  // Owner + servicio + horario + staff
  const owner = await (await fetch(`${API}/auth/register-owner`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: `owner-${Date.now()}@s.test`, password: PASSWORD, name: 'Owner', businessName: 'Salón Sync' }),
  })).json() as { businessId: string; accessToken: string }
  const auth = { 'Content-Type': 'application/json', Authorization: `Bearer ${owner.accessToken}` }
  const svc = await (await fetch(`${API}/businesses/${owner.businessId}/services`, {
    method: 'POST', headers: auth, body: JSON.stringify({ name: 'Corte', durationMinutes: 30, price: 20 }),
  })).json() as { id: string }
  await fetch(`${API}/businesses/${owner.businessId}/hours`, {
    method: 'PUT', headers: auth,
    body: JSON.stringify({ days: [1, 2, 3, 4, 5].map((d) => ({ dayOfWeek: d, isClosed: false, openingTime: '09:00:00', closingTime: '17:00:00' })) }),
  })
  const staff = await (await fetch(`${API}/businesses/${owner.businessId}/staff`)).json() as Array<{ id: string }>

  // Reserva de INVITADO con el teléfono +34…
  const date = nextWeekday(1)
  const slots = await (await fetch(`${API}/businesses/${owner.businessId}/availability?serviceId=${svc.id}&staffId=${staff[0].id}&date=${date}`)).json() as Array<{ start: string }>
  expect(slots.length).toBeGreaterThan(0)
  const guestRes = await fetch(`${API}/reservations`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ businessId: owner.businessId, serviceId: svc.id, staffId: staff[0].id, startTime: slots[0].start, guestName: 'Cliente Sync', guestPhone: fullPhone }),
  })
  expect(guestRes.ok).toBeTruthy()

  // Registro en la UI como cliente con ESE móvil (selector +34 + 9 dígitos)
  await page.goto('/register?type=customer')
  await page.getByTestId('register-name').fill('Cliente Sync')
  await page.getByTestId('register-email').fill(`cli-${Date.now()}@s.test`)
  await page.getByTestId('register-password').fill(PASSWORD)
  await page.getByTestId('register-phone').fill(localPhone)
  await page.getByTestId('register-submit').click()
  // Esperar al aterrizaje autenticado (token guardado) antes de navegar.
  await expect(page).not.toHaveURL(/\/register/)

  // En "Mis reservas" aparece la reserva que hizo como invitado (vinculada por teléfono)
  await page.goto('/mis-reservas')
  await expect(page.getByTestId('reservation-item').filter({ hasText: 'Salón Sync' })).toBeVisible()
})
