import { test, expect } from '@playwright/test'
import type { Page } from '@playwright/test'

/**
 * Guardarraíl responsive: las vistas clave no deben desbordar horizontalmente en un
 * móvil estrecho (375px, iPhone SE). El contenido ancho (tablas, grids, calendarios)
 * debe hacer scroll dentro de su propio contenedor, nunca ensanchar la página.
 * Autosuficiente: registra su propio owner (con servicio) y su propio cliente.
 */

test.use({ viewport: { width: 375, height: 667 } })

const API = 'http://localhost:5000'
const PASSWORD = 'SecurePass123!'

async function registerOwnerWithService(): Promise<{ email: string; businessId: string; businessName: string }> {
  const stamp = `${Date.now()}-${Math.floor(Math.random() * 1e6)}`
  const email = `own-resp-${stamp}@s.test`
  const businessName = `Responsive ${stamp}`
  const owner = await (await fetch(`${API}/auth/register-owner`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password: PASSWORD, name: 'Owner Responsive', businessName }),
  })).json() as { businessId: string; accessToken: string }
  await fetch(`${API}/businesses/${owner.businessId}/services`, {
    method: 'POST', headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${owner.accessToken}` },
    body: JSON.stringify({ name: 'Corte', durationMinutes: 30, price: 20 }),
  })
  return { email, businessId: owner.businessId, businessName }
}

async function registerClient(): Promise<string> {
  const email = `cli-resp-${Date.now()}-${Math.floor(Math.random() * 1e6)}@s.test`
  await fetch(`${API}/auth/register`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password: PASSWORD, name: 'Cliente Responsive' }),
  })
  return email
}

async function login(page: Page, email: string) {
  await page.goto('/login')
  await page.getByTestId('login-email').fill(email)
  await page.getByTestId('login-password').fill(PASSWORD)
  await page.getByTestId('login-submit').click()
  await page.waitForURL(/\/(panel|inicio|explorar)/)
}

async function expectNoHorizontalOverflow(page: Page, name: string) {
  await page.waitForLoadState('networkidle')
  await page.screenshot({ path: `test-results/responsive/${name}.png`, fullPage: true })
  const { scroll, client } = await page.evaluate(() => {
    const doc = document.scrollingElement!
    return { scroll: doc.scrollWidth, client: doc.clientWidth }
  })
  expect(scroll, `${name}: la página desborda (${scroll}px de ${client}px)`).toBeLessThanOrEqual(client + 1)
}

test('páginas públicas sin desborde horizontal en móvil', async ({ page }) => {
  const { businessName } = await registerOwnerWithService()

  await page.goto('/')
  await expectNoHorizontalOverflow(page, 'landing')

  await page.goto('/explorar')
  await expectNoHorizontalOverflow(page, 'explorar')

  // Ficha pública (modal) del negocio recién creado.
  await page.getByTestId('explore-search').fill(businessName)
  const item = page.getByTestId('explore-item').first()
  await expect(item).toBeVisible()
  await item.click()
  await expect(page.getByTestId('business-modal')).toBeVisible()
  await expectNoHorizontalOverflow(page, 'explorar-ficha')

  await page.goto('/login')
  await expectNoHorizontalOverflow(page, 'login')

  await page.goto('/register')
  await expectNoHorizontalOverflow(page, 'register')

  await page.goto('/contacto')
  await expectNoHorizontalOverflow(page, 'contacto')
})

test('vistas del owner sin desborde horizontal en móvil', async ({ page }) => {
  const { email } = await registerOwnerWithService()
  await login(page, email)

  await page.goto('/panel')
  await expectNoHorizontalOverflow(page, 'owner-panel')

  await page.goto('/agenda')
  await expectNoHorizontalOverflow(page, 'owner-agenda')

  // Configuración: abrir TODAS las secciones para auditar su contenido.
  await page.goto('/configuracion')
  for (const section of ['perfil', 'servicios', 'equipo', 'horario', 'festivos', 'modo-reservas', 'confirmacion', 'cancelacion', 'notificaciones', 'plan', 'danger']) {
    const toggle = page.getByTestId(`section-toggle-${section}`)
    if (await toggle.isVisible().catch(() => false)) await toggle.click()
  }
  await expectNoHorizontalOverflow(page, 'owner-configuracion')
})

test('vistas del cliente sin desborde horizontal en móvil', async ({ page }) => {
  const { businessId } = await registerOwnerWithService()
  const email = await registerClient()
  await login(page, email)

  await page.goto('/mis-reservas')
  await expectNoHorizontalOverflow(page, 'cliente-mis-reservas')

  // Wizard de reserva del negocio creado en el setup (tiene un servicio).
  await page.goto(`/reservar?businessId=${businessId}`)
  await expectNoHorizontalOverflow(page, 'cliente-reservar')
})
