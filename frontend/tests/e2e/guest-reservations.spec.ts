import { test, expect } from '@playwright/test'

/**
 * Un invitado (sin cuenta) ve sus reservas en "Mis reservas" verificándose con un
 * código OTP: introduce su teléfono → recibe un código (email/SMS simulado) → el
 * código desbloquea sus reservas.
 *
 * El flujo completo con código real se cubre en los tests de integración del backend
 * (el código solo viaja en el "email/SMS" simulado, no llega al navegador). Aquí se
 * cubre la UI: paso de código, error con código inválido y vuelta atrás.
 */

const API = 'http://localhost:5000'

test('el lookup de invitado pide un código de verificación', async ({ page }) => {
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

  // UI (sin login): teléfono → "Enviar código" → aparece el paso del código.
  await page.goto('/mis-reservas')
  await expect(page.getByTestId('guest-lookup-form')).toBeVisible()
  await page.getByTestId('guest-lookup-phone').fill(phone.slice(3))
  await page.getByTestId('guest-lookup-submit').click()

  await expect(page.getByTestId('guest-otp-form')).toBeVisible()
  await expect(page.getByTestId('guest-otp-sent')).toContainText('código de 6 dígitos')

  // Un código inventado NO desbloquea las reservas: error y sin lista.
  await page.getByTestId('guest-otp-code').fill('000000')
  await page.getByTestId('guest-otp-submit').click()
  await expect(page.getByTestId('guest-lookup-error')).toContainText('código')
  await expect(page.getByTestId('guest-lookup-list')).toHaveCount(0)

  // "Cambiar contacto" vuelve al paso inicial.
  await page.getByTestId('guest-otp-back').click()
  await expect(page.getByTestId('guest-lookup-form')).toBeVisible()
})
