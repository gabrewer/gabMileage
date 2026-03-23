import { expect, test } from '@playwright/test'
import { randomUUID } from 'node:crypto'

/**
 * TASK-005 E2E — Fuel tab UI + Log Fill-up auto-calc
 *
 * Targets the *real* running stack (Gateway + Web). No UI mocking.
 *
 * This spec uses the existing Gateway test-login endpoint to create an isolated user
 * without requiring WorkOS credentials.
 *
 * Contract source of truth: docs/api/fuel-logging.md
 */

async function newAuthedPage({ browser }: { browser: any }) {
  const context = await browser.newContext()
  const page = await context.newPage()

  const userId = `pw-task005-${Date.now()}-${Math.random().toString(16).slice(2)}`
  const resp = await page.goto(
    `/api/auth/test-login?userId=${encodeURIComponent(userId)}&returnTo=${encodeURIComponent(
      '/',
    )}`,
  )

  expect(resp, 'Expected Gateway test-login endpoint to be available for E2E').not.toBeNull()
  expect(resp!.status(), 'Expected /api/auth/test-login to succeed').toBeGreaterThanOrEqual(200)
  expect(resp!.status(), 'Expected /api/auth/test-login to succeed').toBeLessThan(400)

  await page.waitForURL('/')

  return { page, userId }
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

test('Fuel tab shows mpg badge only when mpg is non-null', async ({ browser }) => {
  const { page } = await newAuthedPage({ browser })

  const name = `TASK-005 Fuel ${Date.now()}-${Math.random().toString(16).slice(2)}`
  const vehicleId = await createVehicleViaApi(page, name)

  // First fill-up → mpg likely null (no prior log)
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

  // Second fill-up → mpg should be computed
  await recordFuelLogViaApi(page, vehicleId, {
    commandId: randomUUID(),
    date: '2026-03-15',
    odometer: 10250,
    gallons: 10,
    totalCost: 40,
    pricePerGallon: null,
    calculatedField: 'pricePerGallon',
    isPartialFillUp: false,
    notes: 'E2E seed',
  })

  await page.goto(`/vehicles/${vehicleId}`)

  await expect(page.getByRole('heading', { name })).toBeVisible()

  await expect(page.getByText('Mar 15, 2026')).toBeVisible()
  await expect(page.getByText('Mar 1, 2026')).toBeVisible()

  // Newest log should have mpg; oldest should not. Assert only one mpg badge exists.
  await expect(page.locator('text=/\\bmpg\\b/i')).toHaveCount(1)
})

test('Log Fill-up auto-calculates on blur and applies the inconsistent-all-three rule', async ({
  browser,
}) => {
  const { page } = await newAuthedPage({ browser })

  const name = `TASK-005 Fillup ${Date.now()}-${Math.random().toString(16).slice(2)}`
  const vehicleId = await createVehicleViaApi(page, name)

  await page.goto(`/vehicles/${vehicleId}`)
  await expect(page.getByRole('heading', { name })).toBeVisible()

  // Wait for the loaded empty-state CTA to be enabled.
  const cta = page.getByRole('button', { name: 'Log Fill-up' })
  await expect(cta).toBeEnabled()
  await cta.click()

  await expect(page.getByRole('heading', { name: 'Log Fill-up' })).toBeVisible()

  const gallons = page.getByLabel('Gallons')
  const totalCost = page.getByLabel('Total Cost')
  const pricePerGal = page.getByLabel('Price/gal')
  const odometer = page.getByLabel('Odometer')

  await gallons.fill('10')
  await totalCost.fill('40')

  // Should not calculate on keystroke.
  await expect(pricePerGal).toHaveValue('')

  // Should calculate on blur.
  await odometer.click()
  await expect(pricePerGal).toHaveValue(/4/)
  await expect(page.getByText(/^calc$/i)).toBeVisible()

  // Make it inconsistent and blur again → note should appear and price/gal is recalculated
  // from gallons + totalCost (source of truth).
  await pricePerGal.fill('5')
  await odometer.click()

  await expect(
    page.getByText(
      "These numbers don't quite add up — we'll use gallons and total cost to recalculate price/gal.",
    ),
  ).toBeVisible()

  await expect(pricePerGal).toHaveValue(/4/)
})
