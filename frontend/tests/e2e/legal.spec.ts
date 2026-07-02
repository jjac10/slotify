import { test, expect } from '@playwright/test'

/**
 * Páginas legales públicas (RGPD): /legal/terminos, /legal/privacidad y /legal/cookies
 * son accesibles sin login, muestran su contenido clave y están enlazadas desde el
 * footer de la landing. No requieren backend (contenido estático).
 */

test('los términos y condiciones son públicos y describen el servicio', async ({ page }) => {
  await page.goto('/legal/terminos')

  await expect(page.getByRole('heading', { name: 'Términos y Condiciones de Uso' })).toBeVisible()
  // Objeto del servicio y limitación de responsabilidad (relación cliente ↔ negocio)
  await expect(page.getByTestId('legal-page')).toContainText('plataforma de reservas en línea')
  await expect(page.getByTestId('legal-page')).toContainText('directamente entre el cliente y el negocio')
  // Plan freemium
  await expect(page.getByTestId('legal-page')).toContainText('100 reservas al mes')
})

test('la política de privacidad identifica al responsable y los derechos RGPD', async ({ page }) => {
  await page.goto('/legal/privacidad')

  await expect(page.getByRole('heading', { name: 'Política de Privacidad' })).toBeVisible()
  await expect(page.getByTestId('legal-page')).toContainText('Jose Joaquín Alarcón')
  // Cifrado de datos de invitado y derechos ARSOPL
  await expect(page.getByTestId('legal-page')).toContainText('AES-256-GCM')
  await expect(page.getByTestId('legal-page')).toContainText(
    'acceso, rectificación, supresión, oposición, portabilidad y limitación',
  )
  await expect(page.getByTestId('legal-page')).toContainText('No se ceden datos a terceros')
})

test('la política de cookies explica que solo hay localStorage y no hay banner', async ({ page }) => {
  await page.goto('/legal/cookies')

  await expect(page.getByRole('heading', { name: 'Política de Cookies' })).toBeVisible()
  await expect(page.getByTestId('legal-page')).toContainText('no utiliza cookies de terceros')
  await expect(page.getByTestId('legal-page')).toContainText('localStorage')

  // Navegación entre documentos mediante las pestañas
  await page.getByTestId('legal-tab-terminos').click()
  await expect(page).toHaveURL(/\/legal\/terminos$/)
  await expect(page.getByRole('heading', { name: 'Términos y Condiciones de Uso' })).toBeVisible()
})

test('el footer de la landing enlaza a las páginas legales', async ({ page }) => {
  await page.goto('/')

  await page.getByRole('contentinfo').getByRole('link', { name: 'Privacidad' }).click()
  await expect(page).toHaveURL(/\/legal\/privacidad$/)
  await expect(page.getByRole('heading', { name: 'Política de Privacidad' })).toBeVisible()
})
