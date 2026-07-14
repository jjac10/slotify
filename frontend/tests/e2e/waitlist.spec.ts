import { test, expect } from '@playwright/test'

/**
 * Lista de espera end-to-end: mañana solo hay UN hueco y ya está ocupado → el cliente
 * (registrado) se apunta desde el wizard; la espera aparece en Mi Slotify; cuando el
 * negocio cancela la reserva que llenaba el día, la entrada pasa a "se liberó un hueco"
 * con botón Reservar. Corre contra el stack real.
 */

const API = 'http://localhost:5000'
const PASSWORD = 'SecurePass123!'

function isoDate(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

test('el cliente se apunta a un día completo y se le avisa al liberarse el hueco', async ({ page }) => {
  const suffix = `${Date.now()}-${Math.floor(Math.random() * 1e6)}`

  // --- Setup vía API: mañana (día local) solo abre 9:00–9:30 → 1 hueco de 30 min ---
  const tomorrow = new Date()
  tomorrow.setDate(tomorrow.getDate() + 1)
  const ownerRes = await fetch(`${API}/auth/register-owner`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: `o-${suffix}@s.test`, password: PASSWORD, name: 'Pepe', businessName: `Barbería WL ${suffix}` }),
  })
  const { businessId, accessToken: ownerToken } = await ownerRes.json() as { businessId: string; accessToken: string }
  const svc = await (await fetch(`${API}/businesses/${businessId}/services`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${ownerToken}` },
    body: JSON.stringify({ name: 'Corte', description: null, durationMinutes: 30, price: 20, color: null }),
  })).json() as { id: string }
  await fetch(`${API}/businesses/${businessId}/hours`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${ownerToken}` },
    body: JSON.stringify({ days: [{ dayOfWeek: tomorrow.getDay(), isClosed: false, openingTime: '09:00:00', closingTime: '09:30:00' }] }),
  })
  const staff = await (await fetch(`${API}/businesses/${businessId}/staff`)).json() as Array<{ id: string }>

  // Un invitado ocupa el único hueco (las 9:00 locales de mañana, en UTC).
  const slotLocal = new Date(tomorrow)
  slotLocal.setHours(9, 0, 0, 0)
  const booked = await (await fetch(`${API}/reservations`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ businessId, serviceId: svc.id, staffId: staff[0].id, startTime: slotLocal.toISOString(), guestName: 'Juan', guestPhone: '+34910000061' }),
  })).json() as { id: string }

  // --- El cliente se registra y va al wizard ---
  await page.goto('/register')
  await page.getByTestId('register-account-type').selectOption('customer')
  await page.getByTestId('register-name').fill('Cliente Paciente')
  await page.getByTestId('register-email').fill(`c-${suffix}@s.test`)
  await page.getByTestId('register-password').fill(PASSWORD)
  await page.getByTestId('register-submit').click()
  await expect(page).toHaveURL(/\/inicio$/)

  await page.goto(`/reservar?businessId=${businessId}`)
  await page.getByTestId('service-item').filter({ hasText: 'Corte' }).getByTestId('select-service').click()
  await page.getByTestId('staff-item').first().getByTestId('select-staff').click()

  // Mañana está en la tira de días: sin huecos → botón de lista de espera.
  await page.locator(`[data-testid="date-card"][data-date="${isoDate(tomorrow)}"]`).click()
  await expect(page.getByTestId('reserve-no-slots')).toBeVisible()
  await page.getByTestId('waitlist-join').click()
  await expect(page.getByTestId('waitlist-joined')).toContainText('posición 1')

  // Aparece en Mi Slotify como esperando.
  await page.goto('/inicio')
  const item = page.getByTestId('waitlist-item').filter({ hasText: 'Barbería WL' })
  await expect(item).toContainText('Posición 1')

  // El negocio cancela la reserva que llenaba el día → la espera pasa a "hueco libre".
  const cancel = await fetch(`${API}/reservations/${booked.id}/cancel`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${ownerToken}` },
    body: JSON.stringify({}),
  })
  expect(cancel.ok).toBe(true)

  await page.reload()
  await expect(item).toContainText('Se liberó un hueco', { ignoreCase: true })
  await expect(item.getByTestId('waitlist-book-now')).toBeVisible()

  // Y puede salir de la lista.
  await item.getByTestId('waitlist-leave').click()
  await expect(page.getByTestId('waitlist-item')).toHaveCount(0)
})
