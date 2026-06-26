import { test, expect } from '@playwright/test'

/**
 * Agenda del owner: pestañas Próximas/Pasadas, agrupado por día y filtros
 * (buscar cliente + trabajador). Corre contra el stack real.
 */

const API = 'http://localhost:5000'
const PASSWORD = 'SecurePass123!'

/** "YYYY-MM-DD" del próximo día laboral (Lun–Vie) a N días desde hoy. */
function nextWeekday(offset = 1): string {
  const d = new Date()
  d.setDate(d.getDate() + offset)
  while (d.getDay() === 0 || d.getDay() === 6) d.setDate(d.getDate() + 1)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

test('la agenda separa Próximas/Pasadas, agrupa por día y filtra por cliente', async ({ page }) => {
  const ownerEmail = `owner-agenda-${Date.now()}@s.test`

  // Owner + negocio
  const ownerRes = await fetch(`${API}/auth/register-owner`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: ownerEmail, password: PASSWORD, name: 'Owner', businessName: 'Salón Agenda' }),
  })
  const { businessId, accessToken: ownerToken } = await ownerRes.json() as { businessId: string; accessToken: string }
  const auth = { 'Content-Type': 'application/json', Authorization: `Bearer ${ownerToken}` }

  // Servicio + horario L-V
  const svc = await (await fetch(`${API}/businesses/${businessId}/services`, {
    method: 'POST', headers: auth, body: JSON.stringify({ name: 'Corte', durationMinutes: 30, price: 20 }),
  })).json() as { id: string }
  await fetch(`${API}/businesses/${businessId}/hours`, {
    method: 'PUT', headers: auth,
    body: JSON.stringify({ days: [1, 2, 3, 4, 5].map((d) => ({ dayOfWeek: d, isClosed: false, openingTime: '09:00:00', closingTime: '17:00:00' })) }),
  })
  const staff = await (await fetch(`${API}/businesses/${businessId}/staff`)).json() as Array<{ id: string }>
  const staffId = staff[0].id

  // Primer hueco libre del próximo día laboral → reserva de invitado apuntada por el owner
  const date = nextWeekday(1)
  const slots = await (await fetch(`${API}/businesses/${businessId}/availability?serviceId=${svc.id}&staffId=${staffId}&date=${date}`)).json() as Array<{ start: string }>
  expect(slots.length).toBeGreaterThan(0)
  const created = await fetch(`${API}/reservations`, {
    method: 'POST', headers: auth,
    body: JSON.stringify({ businessId, serviceId: svc.id, staffId, startTime: slots[0].start, guestName: 'Cliente Agenda', guestPhone: '+34600111222' }),
  })
  expect(created.ok).toBeTruthy()

  // Login como owner en la UI
  await page.goto('/login')
  await page.getByTestId('login-email').fill(ownerEmail)
  await page.getByTestId('login-password').fill(PASSWORD)
  await page.getByTestId('login-submit').click()
  await expect(page).toHaveURL(/\/panel/)

  // Agenda: pestañas + reserva agrupada bajo una cabecera de día
  await page.goto('/agenda')
  await expect(page.getByTestId('agenda-tabs')).toBeVisible()
  await expect(page.getByTestId('agenda-day-header').first()).toBeVisible()
  await expect(page.getByTestId('agenda-item').filter({ hasText: 'Cliente Agenda' })).toBeVisible()

  // Agrupado por Semana: la reserva sigue visible bajo una cabecera de semana ("… – …")
  await page.getByTestId('agenda-group-week').click()
  await expect(page.getByTestId('agenda-day-header').first()).toContainText('–')
  await expect(page.getByTestId('agenda-item').filter({ hasText: 'Cliente Agenda' })).toBeVisible()
  await page.getByTestId('agenda-group-day').click()

  // Buscar un nombre que no existe → vacío; limpiar → reaparece
  await page.getByTestId('agenda-search').fill('zzzzz')
  await expect(page.getByTestId('agenda-empty')).toBeVisible()
  await page.getByTestId('agenda-search').fill('')
  await expect(page.getByTestId('agenda-item').filter({ hasText: 'Cliente Agenda' })).toBeVisible()

  // Pestaña Pasadas: no hay nada (la reserva es futura)
  await page.getByTestId('agenda-tab-past').click()
  await expect(page.getByTestId('agenda-empty')).toBeVisible()
})
