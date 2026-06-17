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

/** Devuelve el próximo lunes a partir de hoy como "YYYY-MM-DD". */
function nextMonday(): string {
  const d = new Date()
  const daysUntilMonday = (8 - d.getDay()) % 7 || 7
  d.setDate(d.getDate() + daysUntilMonday)
  return d.toISOString().split('T')[0]
}

test('cliente registrado hace una reserva completa', async ({ page }) => {
  const email = `customer-${Date.now()}@slotify.test`
  const businessId = await setupBusinessWithServiceAndStaff()

  // 1. Registrarse como cliente → aterriza en /mis-reservas
  await page.goto('/register')
  await page.getByTestId('register-account-type').selectOption('customer')
  await page.getByTestId('register-name').fill('Cliente Test')
  await page.getByTestId('register-email').fill(email)
  await page.getByTestId('register-password').fill('SecurePass123!')
  await page.getByTestId('register-submit').click()
  await expect(page).toHaveURL(/\/mis-reservas$/)

  // 2. Ir a reservar con businessId conocido (salta al paso 2: elige servicio)
  await page.goto(`/reservar?businessId=${businessId}`)

  // 3. Seleccionar el primer servicio disponible
  await expect(page.getByTestId('reserve-services')).toBeVisible()
  await page.getByTestId('service-item').first().getByTestId('select-service').click()

  // 4. Seleccionar el primer trabajador (owner-as-staff)
  await expect(page.getByTestId('reserve-staff-list')).toBeVisible()
  await page.getByTestId('staff-item').first().getByTestId('select-staff').click()

  // 5. Elegir el próximo lunes (día laboral en la configuración de horarios)
  await expect(page.getByTestId('reserve-date-input')).toBeVisible()
  await page.getByTestId('reserve-date-input').fill(nextMonday())
  await page.getByTestId('reserve-load-slots').click()

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
