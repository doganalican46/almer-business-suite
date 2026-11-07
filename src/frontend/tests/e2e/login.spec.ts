import { expect, test } from '@playwright/test'

test.beforeEach(async ({ page }) => {
  await page.goto('/')
  await page.evaluate(() => localStorage.clear())
  await page.reload()
})

test('starts in German with an accessible login form', async ({ page }) => {
  await expect(page.locator('html')).toHaveAttribute('lang', 'de-DE')
  await expect(page.getByRole('heading', { name: 'Willkommen zurück' })).toBeVisible()
  await expect(page.getByLabel('E-Mail-Adresse')).toHaveValue('admin@almer.demo')
  await expect(page.getByLabel('Passwort')).toHaveAttribute('type', 'password')
  await expect(page.getByRole('button', { name: 'Sicher anmelden' })).toBeVisible()
})

test('switches the complete login experience to Turkish', async ({ page }) => {
  await page.getByRole('button', { name: 'Sprache wechseln' }).click()
  await expect(page.locator('html')).toHaveAttribute('lang', 'tr-TR')
  await expect(page.getByRole('heading', { name: 'Tekrar hoş geldiniz' })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Güvenli giriş' })).toBeVisible()
})

test('keeps primary actions usable on a narrow viewport', async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 740 })
  await expect(page.getByRole('button', { name: 'Sicher anmelden' })).toBeInViewport()
  await expect(page.getByRole('link', { name: 'Passwort vergessen' })).toBeVisible()
})
