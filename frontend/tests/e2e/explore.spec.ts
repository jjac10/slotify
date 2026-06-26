import { test, expect } from '@playwright/test'

/**
 * Un cliente (sin login) explora negocios, busca por nombre y entra a reservar
 * en uno — sin usar ids. Valida `GET /public/businesses` + Explorar + entrada al flujo.
 */

const BASE_API = 'http://localhost:5000'
const PASSWORD = 'SecurePass123!'

async function createBusiness(name: string): Promise<void> {
  const res = await fetch(`${BASE_API}/auth/register-owner`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: `owner-${Date.now()}-${Math.random()}@slotify.test`, password: PASSWORD, name: 'Owner', businessName: name }),
  })
  if (!res.ok) throw new Error(`register-owner failed: ${await res.text()}`)
  const { businessId, accessToken } = await res.json() as { businessId: string; accessToken: string }

  // Un servicio para que el flujo de reserva tenga algo que mostrar.
  await fetch(`${BASE_API}/businesses/${businessId}/services`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${accessToken}` },
    body: JSON.stringify({ name: 'Corte', description: null, durationMinutes: 30, price: 20, color: null }),
  })
}

test('un cliente explora negocios, busca por nombre y entra a reservar', async ({ page }) => {
  const token = `${Date.now()}`
  const name = `Barbería ${token}`
  await createBusiness(name)

  await page.goto('/explorar')
  await expect(page.getByTestId('explore-list')).toBeVisible()

  // Buscar por nombre
  await page.getByTestId('explore-search').fill(token)
  const item = page.getByTestId('explore-item').filter({ hasText: name })
  await expect(item).toBeVisible()

  // Entrar a reservar en ese negocio
  await item.getByTestId('explore-reserve').click()
  await expect(page).toHaveURL(/\/reservar\?businessId=/)
  await expect(page.getByTestId('reserve-services')).toBeVisible()
})
