import { test, expect } from '@playwright/test'

/**
 * "Mis reservas" paginado en servidor: el toggle Próximas | Pasadas | Todas mapea a
 * ?scope= y "Cargar más" acumula páginas ({ items, total, page, pageSize }).
 * Corre contra el stack real.
 */

const API = 'http://localhost:5000'
const PASSWORD = 'SecurePass123!'

test('Mis reservas separa por scope y "Cargar más" acumula páginas', async ({ page }) => {
  const stamp = Date.now()
  const customerEmail = `cli-pag-${stamp}@s.test`

  // Owner + servicio + staff
  const owner = await (await fetch(`${API}/auth/register-owner`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: `own-pag-${stamp}@s.test`, password: PASSWORD, name: 'Owner', businessName: `Paginación ${stamp}` }),
  })).json() as { businessId: string; accessToken: string }
  const ownerAuth = { 'Content-Type': 'application/json', Authorization: `Bearer ${owner.accessToken}` }
  const svc = await (await fetch(`${API}/businesses/${owner.businessId}/services`, {
    method: 'POST', headers: ownerAuth, body: JSON.stringify({ name: 'Corte', durationMinutes: 30, price: 20 }),
  })).json() as { id: string }
  const staff = await (await fetch(`${API}/businesses/${owner.businessId}/staff`)).json() as Array<{ id: string }>

  // Cliente registrado
  const customer = await (await fetch(`${API}/auth/register`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: customerEmail, password: PASSWORD, name: 'Cliente' }),
  })).json() as { accessToken: string }
  const custAuth = { 'Content-Type': 'application/json', Authorization: `Bearer ${customer.accessToken}` }

  // 21 reservas PASADAS (espaciadas 1 h, sin solapar; el POST no exige que sean futuras)
  // → "Pasadas" pagina (20 + 1 con pageSize por defecto 20)
  for (let i = 0; i < 21; i++) {
    const start = new Date(Date.now() - (i + 2) * 3_600_000).toISOString()
    const r = await fetch(`${API}/reservations`, {
      method: 'POST', headers: custAuth,
      body: JSON.stringify({ businessId: owner.businessId, serviceId: svc.id, staffId: staff[0].id, startTime: start }),
    })
    expect(r.ok).toBeTruthy()
  }
  // + 1 futura → "Próximas" tiene exactamente una
  const future = await fetch(`${API}/reservations`, {
    method: 'POST', headers: custAuth,
    body: JSON.stringify({ businessId: owner.businessId, serviceId: svc.id, staffId: staff[0].id, startTime: new Date(Date.now() + 3 * 86_400_000).toISOString() }),
  })
  expect(future.ok).toBeTruthy()

  // Login como cliente en la UI
  await page.goto('/login')
  await page.getByTestId('login-email').fill(customerEmail)
  await page.getByTestId('login-password').fill(PASSWORD)
  await page.getByTestId('login-submit').click()
  await expect(page).not.toHaveURL(/\/login/)

  // Próximas (default): solo la futura y sin botón de cargar más
  await page.goto('/mis-reservas')
  await expect(page.getByTestId('my-reservations-list')).toBeVisible()
  await expect(page.getByTestId('reservation-item')).toHaveCount(1)
  await expect(page.getByTestId('load-more-reservations')).not.toBeVisible()

  // Pasadas: primera página (20 de 21) + "Cargar más" acumula la segunda
  await page.getByTestId('scope-past').click()
  await expect(page.getByTestId('reservation-item')).toHaveCount(20)
  const loadMore = page.getByTestId('load-more-reservations')
  await expect(loadMore).toContainText('20 de 21')
  await loadMore.click()
  await expect(page.getByTestId('reservation-item')).toHaveCount(21)
  await expect(loadMore).not.toBeVisible()

  // Todas: 22 en total, también en dos páginas (cambiar de pestaña resetea a la 1)
  await page.getByTestId('scope-all').click()
  await expect(page.getByTestId('reservation-item')).toHaveCount(20)
  await page.getByTestId('load-more-reservations').click()
  await expect(page.getByTestId('reservation-item')).toHaveCount(22)
})
