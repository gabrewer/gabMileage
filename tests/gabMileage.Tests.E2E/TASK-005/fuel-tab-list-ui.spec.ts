import { expect, test } from '@playwright/test'
import { randomUUID } from 'node:crypto'

/**
 * TASK-005 E2E — Fuel tab list UI
 *
 * Targets the *real* running stack (Gateway + Web). No mocking.
 *
 * Contract source of truth: docs/api/fuel-logging.md
 */

async function newAuthedPage({ browser }: { browser: any }) {
  const context = await browser.newContext()
  const page = await context.newPage()

  const userId = `pw-task005-${Date.now()}-${Math.random().toString(16).slice(2)}`
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

async function recordFuelLogViaApi(page: any, vehicleId: string, payload: any) {
  const resp = await page.request.post(`/api/v1/garage/vehicles/${vehicleId}/fuel-logs`, {
    data: payload,
  })

  expect([200, 201]).toContain(resp.status())
  const body = (await resp.json()) as { fuelLogId: string; odometerWarning: string | null }
  expect(body.fuelLogId).toMatch(/[0-9a-fA-F-]{36}/)
  return body
}

test('Fuel tab renders newest-first cards with partial/notes pills and action menu', async ({
  browser,
}) => {
  const { page } = await newAuthedPage({ browser })

  const name = `TASK-005 Fuel List ${Date.now()}-${Math.random().toString(16).slice(2)}`
  const vehicleId = await createVehicleViaApi(page, name)

  // Oldest (no tags)
  await recordFuelLogViaApi(page, vehicleId, {
    commandId: randomUUID(),
    date: '2026-03-01',
    odometer: 10000,
    gallons: 10,
    totalCost: 40,
    pricePerGallon: null,
    calculatedField: 'pricePerGallon',
    isPartialFillUp: false,
    notes: null,
  })

  // Newest (partial + notes)
  await recordFuelLogViaApi(page, vehicleId, {
    commandId: randomUUID(),
    date: '2026-03-15',
    odometer: 10250,
    gallons: 10,
    totalCost: 40,
    pricePerGallon: null,
    calculatedField: 'pricePerGallon',
    isPartialFillUp: true,
    notes: 'Quick top-off',
  })

  await page.goto(`/vehicles/${vehicleId}`)
  await expect(page.getByRole('heading', { name })).toBeVisible()

  const history = page.getByLabel('Fuel log history')
  await expect(history).toBeVisible()

  const cards = history.locator('article')
  await expect(cards).toHaveCount(2)

  // Reverse-chron: newest first.
  const first = cards.first()
  await expect(first).toContainText('Mar 15, 2026')
  await expect(first).toContainText(/^partial$/im)
  await expect(first).toContainText(/^notes$/im)

  const second = cards.nth(1)
  await expect(second).toContainText('Mar 1, 2026')
  await expect(second).not.toContainText(/^partial$/im)
  await expect(second).not.toContainText(/^notes$/im)

  // Overflow menu contains Edit/Delete.
  await first.getByLabel('Fuel log actions').click()
  await expect(first.getByRole('button', { name: 'Edit' })).toBeVisible()
  await expect(first.getByRole('button', { name: 'Delete' })).toBeVisible()
})
