import { test, expect } from '@playwright/test'

/**
 * El cliente autenticado puede cancelar y reprogramar sus reservas desde
 * "Mis reservas" usando los botones que aparecen en cada tarjeta próxima.
 */

const API = 'http://localhost:5000'

/** Devuelve "YYYY-MM-DD" del próximo día laboral (Lun–Vie) a N días desde hoy. */
function nextWeekday(offset = 1): string {
  const d = new Date()
  d.setDate(d.getDate() + offset)
  while (d.getDay() === 0 || d.getDay() === 6) d.setDate(d.getDate() + 1)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

interface Setup {
  customerEmail: string
  businessId: string
  serviceId: string
  staffId: string
  reservationId: string
  slotStart: string
}

async function setupReservation(reservationOffset = 1): Promise<Setup> {
  // Owner + negocio
  const ownerRes = await fetch(`${API}/auth/register-owner`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      email: `owner-${Date.now()}@s.test`,
      password: 'SecurePass123!',
      name: 'Owner',
      businessName: 'Test Salón',
    }),
  })
  const { businessId, accessToken: ownerToken } = await ownerRes.json() as { businessId: string; accessToken: string }

  // Servicio
  const svcRes = await fetch(`${API}/businesses/${businessId}/services`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${ownerToken}` },
    body: JSON.stringify({ name: 'Corte', durationMinutes: 30, price: 20 }),
  })
  const { id: serviceId } = await svcRes.json() as { id: string }

  // Horario L-V 09-17
  await fetch(`${API}/businesses/${businessId}/hours`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${ownerToken}` },
    body: JSON.stringify({
      days: [1, 2, 3, 4, 5].map((d) => ({ dayOfWeek: d, isClosed: false, openingTime: '09:00:00', closingTime: '17:00:00' })),
    }),
  })

  // Staff (owner-as-staff)
  const staffRes = await fetch(`${API}/businesses/${businessId}/staff`)
  const staff = await staffRes.json() as Array<{ id: string }>
  const staffId = staff[0].id

  // Fecha y primer slot disponible
  const date = nextWeekday(reservationOffset)
  const availRes = await fetch(
    `${API}/businesses/${businessId}/availability?serviceId=${serviceId}&staffId=${staffId}&date=${date}`,
  )
  const slots = await availRes.json() as Array<{ start: string }>
  if (slots.length === 0) throw new Error(`No slots for ${date}`)
  const slotStart = slots[0].start

  // Cliente
  const customerEmail = `customer-${Date.now()}@s.test`
  const custRes = await fetch(`${API}/auth/register`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: customerEmail, password: 'SecurePass123!', name: 'Cliente Test' }),
  })
  const { accessToken: customerToken } = await custRes.json() as { accessToken: string }

  // Reserva autenticada
  const resRes = await fetch(`${API}/reservations`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${customerToken}` },
    body: JSON.stringify({ businessId, serviceId, staffId, startTime: slotStart }),
  })
  if (!resRes.ok) throw new Error(`create reservation failed: ${await resRes.text()}`)
  const { id: reservationId } = await resRes.json() as { id: string }

  return { customerEmail, businessId, serviceId, staffId, reservationId, slotStart }
}

test('el cliente cancela su propia reserva desde Mis reservas', async ({ page }) => {
  const { customerEmail } = await setupReservation()

  // Login
  await page.goto('/login')
  await page.getByTestId('login-email').fill(customerEmail)
  await page.getByTestId('login-password').fill('SecurePass123!')
  await page.getByTestId('login-submit').click()
  await expect(page).toHaveURL(/\/inicio$/)

  // Ir a Mis reservas
  await page.goto('/mis-reservas')
  await expect(page.getByTestId('my-reservations-list')).toBeVisible()

  // Ver el botón de cancelar en la primera reserva próxima
  const item = page.getByTestId('reservation-item').first()
  await expect(item.getByTestId('cancel-btn')).toBeVisible()

  // Solicitar cancelación
  await item.getByTestId('cancel-btn').click()

  // Confirmar
  await expect(item.getByTestId('cancel-confirm-btn')).toBeVisible()
  await item.getByTestId('cancel-confirm-btn').click()

  // La reserva desaparece de la lista (o la lista pasa a vacío)
  await expect(item).not.toBeVisible()
})

test('el cliente reprograma su reserva desde Mis reservas', async ({ page }) => {
  const { customerEmail, businessId, serviceId, staffId } = await setupReservation()

  // Login
  await page.goto('/login')
  await page.getByTestId('login-email').fill(customerEmail)
  await page.getByTestId('login-password').fill('SecurePass123!')
  await page.getByTestId('login-submit').click()
  await expect(page).toHaveURL(/\/inicio$/)

  // Ir a Mis reservas
  await page.goto('/mis-reservas')
  await expect(page.getByTestId('my-reservations-list')).toBeVisible()

  // Ver el botón de reprogramar
  const item = page.getByTestId('reservation-item').first()
  await expect(item.getByTestId('reschedule-btn')).toBeVisible()
  await item.getByTestId('reschedule-btn').click()

  // El modal se abre con el calendario mensual
  await expect(page.getByTestId('month-calendar')).toBeVisible()

  // Elegir un día diferente (3 días laborales en el futuro para evitar solapamiento)
  const newDate = nextWeekday(3)
  const dayBtn = page.locator(`[data-testid="calendar-day"][data-date="${newDate}"]`)
  // El calendario abre en el mes de la reserva; si el día cae en el mes siguiente, navegar.
  if (!(await dayBtn.isVisible())) {
    await page.getByLabel('Mes siguiente').click()
  }
  await dayBtn.click()

  // Esperar slots disponibles
  await expect(page.getByTestId('reschedule-slots')).toBeVisible({ timeout: 10000 })

  // Elegir el primer slot
  await page.getByTestId('reschedule-slot').first().click()

  // El modal se cierra y la reserva se actualiza en la lista
  await expect(page.getByTestId('month-calendar')).not.toBeVisible()
  await expect(page.getByTestId('my-reservations-list')).toBeVisible()
})
