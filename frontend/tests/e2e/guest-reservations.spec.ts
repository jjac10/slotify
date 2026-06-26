import { test, expect } from '@playwright/test'

/**
 * Un invitado (sin cuenta) reservó con su teléfono y luego ve sus reservas en
 * "Mis reservas" buscando por ese teléfono — sin login.
 */

const API = 'http://localhost:5000'

test('un invitado busca sus reservas por teléfono', async ({ page }) => {
  const phone = `+34600${Math.floor(100000 + Math.random() * 899999)}`

  // Setup vía API: owner + servicio + una reserva de invitado con ese teléfono.
  const ownerRes = await fetch(`${API}/auth/register-owner`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: `o-${Date.now()}@s.test`, password: 'SecurePass123!', name: 'Pepe', businessName: 'Barbería Test' }),
  })
  const { businessId, accessToken } = await ownerRes.json() as { businessId: string; accessToken: string }
  const svc = await (await fetch(`${API}/businesses/${businessId}/services`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${accessToken}` },
    body: JSON.stringify({ name: 'Corte', description: null, durationMinutes: 30, price: 20, color: null }),
  })).json() as { id: string }
  const staff = await (await fetch(`${API}/businesses/${businessId}/staff`)).json() as Array<{ id: string }>

  const future = new Date()
  future.setDate(future.getDate() + 2)
  future.setUTCHours(10, 0, 0, 0)
  const r = await fetch(`${API}/reservations`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ businessId, serviceId: svc.id, staffId: staff[0].id, startTime: future.toISOString(), guestName: 'Juan', guestPhone: phone }),
  })
  if (!r.ok) throw new Error(`create-reservation failed: ${await r.text()}`)

  // UI (sin login): buscar por teléfono (modo teléfono por defecto; +34 del selector + 9 dígitos).
  await page.goto('/mis-reservas')
  await expect(page.getByTestId('guest-lookup-form')).toBeVisible()
  await page.getByTestId('guest-lookup-phone').fill(phone.slice(3))
  await page.getByTestId('guest-lookup-submit').click()

  await expect(page.getByTestId('guest-lookup-list')).toBeVisible()
  await expect(page.getByTestId('reservation-item').filter({ hasText: 'Barbería Test' })).toBeVisible()
})
