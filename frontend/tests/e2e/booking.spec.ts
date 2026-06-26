import { test, expect } from '@playwright/test'

/**
 * Cliente registrado hace una reserva completa: elige servicio → staff → fecha →
 * slot → crea reserva → la ve en "mis reservas".
 *
 * Precondición: existe un negocio con servicio, staff y horario (creados vía API).
 */

const BASE_API = 'http://localhost:5000'

async function setupBusinessWithServiceAndStaff(): Promise<string> {
  // Registrar owner + crear negocio
  const ownerRes = await fetch(`${BASE_API}/auth/register-owner`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      email: `owner-${Date.now()}@slotify.test`,
      password: 'SecurePass123!',
      name: 'Owner Test',
      businessName: 'Barbería Test',
    }),
  })
  if (!ownerRes.ok) throw new Error(`register-owner failed: ${await ownerRes.text()}`)
  const ownerData = await ownerRes.json() as { businessId: string; accessToken: string }
  const businessId = ownerData.businessId
  const ownerToken = ownerData.accessToken

  // Crear servicio (30 min, 25 €)
  const svcRes = await fetch(`${BASE_API}/businesses/${businessId}/services`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${ownerToken}`,
    },
    body: JSON.stringify({
      name: 'Corte de cabello',
      description: 'Corte clásico',
      durationMinutes: 30,
      price: 25,
      color: '#FF5733',
    }),
  })
  if (!svcRes.ok) throw new Error(`create-service failed: ${await svcRes.text()}`)

  // Configurar horario lunes-viernes 09:00-17:00
  // SetBusinessHoursRequest: { days: BusinessHourInput[] }
  // BusinessHourInput: { dayOfWeek, isClosed, openingTime, closingTime } (TimeOnly "HH:mm:ss")
  const hoursRes = await fetch(`${BASE_API}/businesses/${businessId}/hours`, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${ownerToken}`,
    },
    body: JSON.stringify({
      days: [
        { dayOfWeek: 1, isClosed: false, openingTime: '09:00:00', closingTime: '17:00:00' },
        { dayOfWeek: 2, isClosed: false, openingTime: '09:00:00', closingTime: '17:00:00' },
        { dayOfWeek: 3, isClosed: false, openingTime: '09:00:00', closingTime: '17:00:00' },
        { dayOfWeek: 4, isClosed: false, openingTime: '09:00:00', closingTime: '17:00:00' },
        { dayOfWeek: 5, isClosed: false, openingTime: '09:00:00', closingTime: '17:00:00' },
      ],
    }),
  })
  if (!hoursRes.ok) throw new Error(`set-hours failed: ${await hoursRes.text()}`)

  return businessId
}

/**
 * Primer día laboral (Lun–Vie) futuro dentro de la tira de 7 días (hoy..hoy+6),
 * en formato local "YYYY-MM-DD" para casar con el data-date de las date-cards.
 */
function futureWeekdayInStrip(): string {
  const base = new Date()
  for (let i = 1; i < 7; i++) {
    const d = new Date(base.getFullYear(), base.getMonth(), base.getDate() + i)
    if (d.getDay() >= 1 && d.getDay() <= 5) {
      return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
    }
  }
  throw new Error('no se encontró día laboral en la tira')
}

test('cliente registrado hace una reserva completa', async ({ page }) => {
  const email = `customer-${Date.now()}@slotify.test`
  const businessId = await setupBusinessWithServiceAndStaff()

  // 1. Registrarse como cliente → aterriza en su inicio (Mi Slotify)
  await page.goto('/register')
  await page.getByTestId('register-account-type').selectOption('customer')
  await page.getByTestId('register-name').fill('Cliente Test')
  await page.getByTestId('register-email').fill(email)
  await page.getByTestId('register-password').fill('SecurePass123!')
  await page.getByTestId('register-submit').click()
  await expect(page).toHaveURL(/\/inicio$/)

  // 2. Ir a reservar con businessId conocido (salta al paso 2: elige servicio)
  await page.goto(`/reservar?businessId=${businessId}`)

  // 3. Seleccionar el primer servicio disponible
  await expect(page.getByTestId('reserve-services')).toBeVisible()
  await page.getByTestId('service-item').first().getByTestId('select-service').click()

  // 4. Seleccionar el primer trabajador (owner-as-staff)
  await expect(page.getByTestId('reserve-staff-list')).toBeVisible()
  await page.getByTestId('staff-item').first().getByTestId('select-staff').click()

  // 5. Elegir un día laboral en la tira de días → cargan los horarios disponibles
  await expect(page.getByTestId('reserve-days')).toBeVisible()
  await page.locator(`[data-testid="date-card"][data-date="${futureWeekdayInStrip()}"]`).click()

  // 6. Seleccionar el primer slot disponible (usuario autenticado → crea reserva directamente)
  await expect(page.getByTestId('reserve-slots')).toBeVisible()
  await page.getByTestId('slot-item').first().getByTestId('select-slot').click()

  // 7. La reserva se crea y aparece la confirmación
  await expect(page.getByTestId('booking-confirmed')).toBeVisible()

  // 8. Verificar en "mis reservas" que la reserva existe
  await page.goto('/mis-reservas')
  await expect(page.getByTestId('my-reservations-list')).toBeVisible()
  const reservations = await page.getByTestId('reservation-item').all()
  expect(reservations.length).toBeGreaterThan(0)
})
