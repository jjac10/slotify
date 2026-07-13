import { test, expect } from '@playwright/test'

/**
 * Tiempo real (SignalR): un cliente con "Mis reservas" abierta ve su reserva pasar de
 * Pendiente a Confirmada cuando el negocio la confirma — SIN recargar la página.
 * Corre contra el stack real (WebSocket vía el proxy /api de Vite).
 */

const API = 'http://localhost:5000'
const PASSWORD = 'SecurePass123!'

test('la reserva del cliente se confirma en vivo, sin recargar', async ({ page }) => {
  const suffix = `${Date.now()}-${Math.floor(Math.random() * 1e6)}`

  // Setup vía API: negocio con confirmación MANUAL + un servicio.
  const ownerRes = await fetch(`${API}/auth/register-owner`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: `o-${suffix}@s.test`, password: PASSWORD, name: 'Pepe', businessName: `Barbería RT ${suffix}` }),
  })
  const { businessId, accessToken: ownerToken } = await ownerRes.json() as { businessId: string; accessToken: string }
  await fetch(`${API}/businesses/${businessId}/confirmation-mode`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${ownerToken}` },
    body: JSON.stringify({ mode: 'manual' }),
  })
  const svc = await (await fetch(`${API}/businesses/${businessId}/services`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${ownerToken}` },
    body: JSON.stringify({ name: 'Corte', description: null, durationMinutes: 30, price: 20, color: null }),
  })).json() as { id: string }
  const staff = await (await fetch(`${API}/businesses/${businessId}/staff`)).json() as Array<{ id: string }>

  // Cliente registrado por UI (deja el token en localStorage).
  await page.goto('/register')
  await page.getByTestId('register-account-type').selectOption('customer')
  await page.getByTestId('register-name').fill('Cliente En Vivo')
  await page.getByTestId('register-email').fill(`c-${suffix}@s.test`)
  await page.getByTestId('register-password').fill(PASSWORD)
  await page.getByTestId('register-submit').click()
  await expect(page).toHaveURL(/\/inicio$/)

  // Reserva del cliente vía API con SU token → queda 'pending' (modo manual).
  const customerToken = await page.evaluate(() => localStorage.getItem('slotify.accessToken'))
  const future = new Date()
  future.setDate(future.getDate() + 3)
  future.setUTCHours(10, 0, 0, 0)
  const booked = await (await fetch(`${API}/reservations`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${customerToken}` },
    body: JSON.stringify({ businessId, serviceId: svc.id, staffId: staff[0].id, startTime: future.toISOString() }),
  })).json() as { id: string; status: string }
  expect(booked.status).toBe('pending')

  // El cliente mira su lista: Pendiente.
  await page.goto('/mis-reservas')
  const item = page.getByTestId('reservation-item').filter({ hasText: 'Barbería RT' })
  await expect(item).toContainText('Pendiente')

  // El negocio confirma por detrás… y la pantalla se actualiza sola (SignalR).
  const confirm = await fetch(`${API}/reservations/${booked.id}/confirm`, {
    method: 'POST',
    headers: { Authorization: `Bearer ${ownerToken}` },
  })
  expect(confirm.ok).toBe(true)

  await expect(item).toContainText('Confirmada', { timeout: 10_000 })
})
