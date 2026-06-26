import { test, expect } from '@playwright/test'

/**
 * Cuentas de empleado: el owner invita a un empleado (genera un enlace); el empleado
 * abre el enlace, fija su contraseña y entra → ve la Agenda pero NO la Configuración.
 * Corre contra el stack real.
 */

const API = 'http://localhost:5000'
const PASSWORD = 'SecurePass123!'

test('un empleado acepta su invitación y entra a su agenda (sin configuración)', async ({ page }) => {
  const stamp = Date.now()

  // Owner + premium (Free solo permite 1 trabajador) + empleado con email, vía API
  const owner = await (await fetch(`${API}/auth/register-owner`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: `own-${stamp}@s.test`, password: PASSWORD, name: 'Lucía', businessName: 'Barbería Equipo' }),
  })).json() as { businessId: string; accessToken: string; businessRole: string }
  expect(owner.businessRole).toBe('owner')
  const auth = { 'Content-Type': 'application/json', Authorization: `Bearer ${owner.accessToken}` }
  await fetch(`${API}/businesses/${owner.businessId}/plan`, { method: 'PUT', headers: auth, body: JSON.stringify({ code: 'premium' }) })
  const empEmail = `emp-${stamp}@s.test`
  const emp = await (await fetch(`${API}/businesses/${owner.businessId}/staff`, {
    method: 'POST', headers: auth, body: JSON.stringify({ name: 'Marta', email: empEmail }),
  })).json() as { id: string }
  const invite = await (await fetch(`${API}/businesses/${owner.businessId}/staff/${emp.id}/invite`, { method: 'POST', headers: auth, body: '{}' }))
    .json() as { token: string }
  expect(invite.token).toBeTruthy()

  // El empleado abre el enlace de invitación, fija contraseña y entra
  await page.goto(`/invitacion/${invite.token}`)
  await expect(page.getByTestId('accept-invite-form')).toBeVisible()
  await expect(page.getByTestId('invite-email')).toHaveValue(empEmail)
  await page.getByTestId('invite-password').fill('EmpSecure123!')
  await page.getByTestId('accept-submit').click()

  // Aterriza en su agenda; el nav muestra Agenda pero NO Configuración (no es owner)
  await expect(page).toHaveURL(/\/agenda/)
  await expect(page.getByRole('heading', { name: 'Mi agenda' })).toBeVisible()
  await expect(page.getByTestId('nav-agenda')).toBeVisible()
  await expect(page.getByTestId('nav-settings')).toHaveCount(0)
})
