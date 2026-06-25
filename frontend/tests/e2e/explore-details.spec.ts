import { test, expect } from '@playwright/test'

/**
 * En Explorar, al pulsar el nombre de un negocio se abre un modal con sus datos.
 * Para un negocio en "solo calendario" muestra el teléfono para llamar (no reserva online).
 * Corre contra el stack real.
 */

const API = 'http://localhost:5000'
const PASSWORD = 'SecurePass123!'

test('el modal de Explorar muestra el contacto de un negocio "solo calendario"', async ({ page }) => {
  const stamp = Date.now()
  const businessName = `Peluquería Detalles ${stamp}`
  const phone = '+34 911 22 33 44'

  // Owner + perfil (teléfono/dirección) + modo solo calendario, vía API
  const owner = await (await fetch(`${API}/auth/register-owner`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: `own-${stamp}@s.test`, password: PASSWORD, name: 'Ana', businessName }),
  })).json() as { businessId: string; accessToken: string }
  const auth = { 'Content-Type': 'application/json', Authorization: `Bearer ${owner.accessToken}` }
  await fetch(`${API}/businesses/${owner.businessId}/profile`, {
    method: 'PUT', headers: auth,
    body: JSON.stringify({ category: 'peluqueria', photoUrl: null, latitude: null, longitude: null, phone, address: 'Calle Mayor 1' }),
  })
  await fetch(`${API}/businesses/${owner.businessId}/services`, {
    method: 'POST', headers: auth, body: JSON.stringify({ name: 'Corte de pelo', durationMinutes: 30, price: 15 }),
  })
  await fetch(`${API}/businesses/${owner.businessId}/booking-mode`, { method: 'PUT', headers: auth, body: JSON.stringify({ mode: 'calendar_only' }) })

  // Como visitante, busca el negocio y abre su modal
  await page.goto('/explorar')
  await page.getByTestId('explore-search').fill(businessName)
  const card = page.getByTestId('explore-item').filter({ hasText: businessName })
  await expect(card).toBeVisible()
  await card.getByTestId('explore-details').click()

  // El modal muestra el contacto y el aviso de "no reserva online" (sin botón Reservar)
  const modal = page.getByTestId('business-modal')
  await expect(modal).toBeVisible()
  await expect(modal.getByTestId('business-modal-in-person')).toBeVisible()
  await expect(modal.getByTestId('business-modal-phone')).toContainText('911 22 33 44')
  await expect(modal.getByTestId('business-modal-address')).toContainText('Calle Mayor 1')
  await expect(modal.getByTestId('business-modal-reserve')).toHaveCount(0)

  // Y los servicios (con precio/duración) y el equipo
  const servicesBox = modal.getByTestId('business-modal-services')
  await expect(servicesBox).toContainText('Corte de pelo')
  await expect(servicesBox).toContainText('30 min')
  await expect(servicesBox).toContainText('15,00')
  await expect(modal.getByTestId('business-modal-staff')).toContainText('Ana')

  // Se cierra
  await page.getByTestId('business-modal-close').click()
  await expect(modal).toHaveCount(0)
})
