import { test, expect } from '@playwright/test'

/**
 * Reseñas (una por negocio, editable): un cliente valora un negocio desde una reserva
 * pasada, luego ve "Ya valoraste" + puede editar, y la reseña aparece en "Mis reseñas".
 * Corre contra el stack real.
 */

const API = 'http://localhost:5000'
const PASSWORD = 'SecurePass123!'

test('el cliente valora un negocio, lo edita y lo ve en "Mis reseñas"', async ({ page }) => {
  const stamp = Date.now()
  const customerEmail = `cli-rev-${stamp}@s.test`
  const businessName = `Barbería Reseñas ${stamp}`

  // Owner + servicio + staff
  const owner = await (await fetch(`${API}/auth/register-owner`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: `own-${stamp}@s.test`, password: PASSWORD, name: 'Owner', businessName }),
  })).json() as { businessId: string; accessToken: string }
  const ownerAuth = { 'Content-Type': 'application/json', Authorization: `Bearer ${owner.accessToken}` }
  const svc = await (await fetch(`${API}/businesses/${owner.businessId}/services`, {
    method: 'POST', headers: ownerAuth, body: JSON.stringify({ name: 'Corte', durationMinutes: 30, price: 20 }),
  })).json() as { id: string }
  const staff = await (await fetch(`${API}/businesses/${owner.businessId}/staff`)).json() as Array<{ id: string }>

  // Cliente registrado + reserva PASADA (el POST no exige que sea futura)
  const customer = await (await fetch(`${API}/auth/register`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: customerEmail, password: PASSWORD, name: 'Cliente' }),
  })).json() as { accessToken: string }
  const past = new Date(Date.now() - 2 * 86_400_000).toISOString()
  const booking = await fetch(`${API}/reservations`, {
    method: 'POST', headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${customer.accessToken}` },
    body: JSON.stringify({ businessId: owner.businessId, serviceId: svc.id, staffId: staff[0].id, startTime: past }),
  })
  expect(booking.ok).toBeTruthy()

  // Login como cliente en la UI
  await page.goto('/login')
  await page.getByTestId('login-email').fill(customerEmail)
  await page.getByTestId('login-password').fill(PASSWORD)
  await page.getByTestId('login-submit').click()
  await expect(page).not.toHaveURL(/\/login/)

  // Mis reservas → Pasadas → Valorar
  await page.goto('/mis-reservas')
  await page.getByRole('button', { name: 'Pasadas' }).click()
  const card = page.getByTestId('reservation-item').filter({ hasText: businessName })
  await expect(card).toBeVisible()
  await card.getByTestId('review-btn').click()

  // Modal: 5 estrellas + comentario + enviar
  await expect(page.getByTestId('review-modal')).toBeVisible()
  await page.getByTestId('review-star-5').click()
  await page.getByTestId('review-comment').fill('Muy buen trato')
  await page.getByTestId('review-submit').click()

  // Tras valorar: la tarjeta muestra "Ya valoraste" + opción de editar
  await expect(card.getByTestId('reviewed-badge')).toBeVisible()
  await expect(card.getByTestId('review-edit-btn')).toBeVisible()

  // "Mis reseñas": aparece la reseña del negocio
  await page.goto('/mis-resenas')
  const reviewItem = page.getByTestId('my-review-item').filter({ hasText: businessName })
  await expect(reviewItem).toBeVisible()
  await expect(reviewItem).toContainText('Muy buen trato')

  // Editar desde "Mis reseñas"
  await reviewItem.getByTestId('my-review-edit-btn').click()
  await expect(page.getByTestId('review-modal')).toBeVisible()
  await page.getByTestId('review-comment').fill('Lo pensé mejor: regular')
  await page.getByTestId('review-star-2').click()
  await page.getByTestId('review-submit').click()
  await expect(page.getByTestId('my-review-item').filter({ hasText: businessName })).toContainText('Lo pensé mejor: regular')
})
