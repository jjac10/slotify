import { test, expect } from '@playwright/test'

/**
 * Vista de día (timeline) de la Agenda: muestra las reservas del día como bloques y
 * un clic en un hueco libre abre "nueva reserva" con ese día. Corre contra el stack real.
 */

const API = 'http://localhost:5000'
const PASSWORD = 'SecurePass123!'

test('la Agenda en vista Día muestra las reservas como bloques y permite añadir', async ({ page }) => {
  const stamp = Date.now()
  const owner = await (await fetch(`${API}/auth/register-owner`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: `own-day-${stamp}@s.test`, password: PASSWORD, name: 'Owner', businessName: `Día ${stamp}` }),
  })).json() as { businessId: string; accessToken: string }
  const auth = { 'Content-Type': 'application/json', Authorization: `Bearer ${owner.accessToken}` }
  const svc = await (await fetch(`${API}/businesses/${owner.businessId}/services`, {
    method: 'POST', headers: auth, body: JSON.stringify({ name: 'Corte', durationMinutes: 30, price: 20 }),
  })).json() as { id: string }
  const staff = await (await fetch(`${API}/businesses/${owner.businessId}/staff`)).json() as Array<{ id: string }>

  // Reserva HOY a las 10:00 (hora local)
  const at10 = new Date(); at10.setHours(10, 0, 0, 0)
  const created = await fetch(`${API}/reservations`, {
    method: 'POST', headers: auth,
    body: JSON.stringify({ businessId: owner.businessId, serviceId: svc.id, staffId: staff[0].id, startTime: at10.toISOString(), guestName: 'Cliente Día', guestPhone: '+34600999888' }),
  })
  expect(created.ok).toBeTruthy()

  // Login owner
  await page.goto('/login')
  await page.getByTestId('login-email').fill(`own-day-${stamp}@s.test`)
  await page.getByTestId('login-password').fill(PASSWORD)
  await page.getByTestId('login-submit').click()
  await expect(page).toHaveURL(/\/panel/)

  // Agenda → vista Día → bloque visible (hoy es el día por defecto)
  await page.goto('/agenda')
  await page.getByTestId('agenda-view-day').click()
  await expect(page.getByTestId('agenda-day-timeline')).toBeVisible()
  await expect(page.getByTestId('agenda-day-block').filter({ hasText: 'Cliente Día' })).toBeVisible()

  // Clic en un hueco libre → abre "nueva reserva" con el día puesto
  await page.getByTestId('agenda-day-slot').first().click()
  await expect(page.getByTestId('new-reservation-modal')).toBeVisible()
})
