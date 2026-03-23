import { expect, test } from '@playwright/test'
import { randomUUID } from 'node:crypto'

/**
 * TASK-007 E2E — Fuel log log/edit/delete smoke
 *
 * Targets the *real* running stack (Gateway + Web). No mocking.
 *
 * Auth strategy:
 * - Uses the Gateway test-login endpoint to create an isolated user without WorkOS credentials.
 *
 * Breadcrumbs (backend-builder)
 * Who: backend-builder
 * What: Add a single Playwright flow that logs a fill-up via the UI, then edits and deletes it.
 * Why: TASK-007 acceptance requires a UI-level check for the Sprint 03 fuel log lifecycle (log/edit/delete).
 * Alternatives considered: only API-seeding logs (rejected: would not validate the Log Fill-up UI submit wiring).
 * Confidence: medium
 */

async function newAuthedPage({ browser }: { browser: any }) {
  const context = await browser.newContext()
  const page = await context.newPage()

  const userId = `pw-task007-fuel-${Date.now()}-${Math.random().toString(16).slice(2)}`
  const resp = await page.goto(
    `/api/auth/test-login?userId=${encodeURIComponent(userId)}&returnTo=${encodeURIComponent('/')}`,
  )

  expect(resp, 'Expected Gateway test-login endpoint to be available for E2E').not.toBeNull()
  expect(resp!.status(), 'Expected /api/auth/test-login to succeed').toBeGreaterThanOrEqual(200)
  expect(resp!.status(), 'Expected /api/auth/test-login to succeed').toBeLessThan(400)

  await page.waitForURL('/')

  return { page }
}

async function createVehicleViaApi(page: any, name: string) {
  const resp = await page.request.post('/api/v1/garage/vehicles', {
    data: {
      commandId: randomUUID(),
      name,
      make: null,
      model: null,
      year: null,
    },
  })

  expect(resp.ok(), 'Expected POST /garage/vehicles to succeed').toBeTruthy()
  const body = (await resp.json()) as { vehicleId: string }
  expect(body.vehicleId).toMatch(/[0-9a-fA-F-]{36}/)
  return body.vehicleId
}

test('Log Fill-up: user can log, edit, and delete a fill-up and see it reflected in the Fuel tab', async ({
  browser,
}) => {
  const { page } = await newAuthedPage({ browser })

  const vehicleName = `TASK-007 Fuel Lifecycle ${Date.now()}`
  const vehicleId = await createVehicleViaApi(page, vehicleName)

  await page.goto(`/vehicles/${vehicleId}`)
  await expect(page.getByRole('heading', { name: vehicleName })).toBeVisible()

  // Log a fill-up via UI (this is the key missing coverage vs TASK-006).
  // Mirror TASK-005: the CTA is disabled until the empty-state loader completes.
  const cta = page.getByRole('button', { name: /^log fill-up$/i })
  await expect(cta).toBeEnabled()
  await cta.click()

  await page.waitForURL(`/vehicles/${vehicleId}/fuel/log`)
  await expect(page.getByRole('heading', { name: 'Log Fill-up' })).toBeVisible()

  await page.getByLabel('Date').fill('2026-03-23')
  await page.getByLabel('Odometer').fill('45230')

  await page.getByLabel('Gallons').fill('10')
  await page.getByLabel('Total Cost').fill('40')

  // Blur triggers auto-calc.
  await page.getByLabel('Odometer').click()
  await expect(page.getByLabel('Price/gal')).toHaveValue(/4/)

  await page.getByRole('button', { name: /^save$/i }).click()

  await expect(page.getByText('Fill-up saved')).toBeVisible()
  await page.waitForURL(`/vehicles/${vehicleId}`)

  const history = page.getByLabel('Fuel log history')
  await expect(history).toBeVisible()

  const createdCard = history.locator('article').filter({ hasText: /45[,]?230\s*mi/i }).first()
  await expect(createdCard).toBeVisible()

  // Edit the log.
  await createdCard.getByLabel('Fuel log actions').click()
  await createdCard.getByRole('button', { name: /^edit$/i }).click()

  await expect(page.getByRole('heading', { name: 'Edit Fill-up' })).toBeVisible()
  await expect(page.getByLabel('Odometer')).toHaveValue('45230')

  await page.getByLabel('Notes').fill('edited via TASK-007')
  await page.getByRole('button', { name: /^save changes$/i }).click()

  await expect(page.getByText('Changes saved')).toBeVisible()
  await page.waitForURL(`/vehicles/${vehicleId}`)

  const editedCard = history.locator('article').filter({ hasText: /edited via TASK-007/i }).first()
  await expect(editedCard).toBeVisible()

  // Delete the log.
  await editedCard.getByLabel('Fuel log actions').click()
  await editedCard.getByRole('button', { name: /^delete$/i }).click()

  const dialog = page.getByRole('dialog')
  await expect(dialog).toBeVisible()
  await dialog.getByRole('button', { name: /^delete$/i }).click()

  await expect(page.getByText('Fill-up deleted')).toBeVisible()

  // Entry removed from list.
  await expect(history.locator('article').filter({ hasText: /45[,]?230\s*mi/i })).toHaveCount(0)
})
