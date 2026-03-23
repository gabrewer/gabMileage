import { expect, test } from '@playwright/test'
import { randomUUID } from 'node:crypto'

/**
 * TASK-006 E2E — Fuel log edit/delete flows
 *
 * Targets the *real* running stack (Gateway + Web). No mocking.
 *
 * Auth strategy:
 * - Uses the Gateway test-login endpoint to create an isolated user without WorkOS credentials.
 *
 * Contract source of truth: docs/api/fuel-logging.md
 */

async function newAuthedPage({ browser }: { browser: any }) {
  const context = await browser.newContext()
  const page = await context.newPage()

  const userId = `pw-task006-${Date.now()}-${Math.random().toString(16).slice(2)}`
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

function yyyyMmDd(d: Date) {
  const y = d.getFullYear()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${y}-${m}-${day}`
}

function subtractDays(d: Date, days: number) {
  const copy = new Date(d)
  copy.setDate(copy.getDate() - days)
  return copy
}

test('Edit: pre-populates fields (including calc label) and saving refreshes list order', async ({
  browser,
}) => {
  const { page } = await newAuthedPage({ browser })

  const vehicleName = `TASK-006 Edit Fuel ${Date.now()}`
  const vehicleId = await createVehicleViaApi(page, vehicleName)

  const today = new Date()
  const d1 = yyyyMmDd(subtractDays(today, 30))
  const d2 = yyyyMmDd(subtractDays(today, 15))

  // Seed 3 logs, with a same-date tie so odometer tie-breaker matters.
  await recordFuelLogViaApi(page, vehicleId, {
    commandId: randomUUID(),
    date: d1,
    odometer: 10000,
    gallons: 10,
    totalCost: 40,
    pricePerGallon: null,
    calculatedField: 'pricePerGallon',
    isPartialFillUp: false,
    notes: null,
  })

  await recordFuelLogViaApi(page, vehicleId, {
    commandId: randomUUID(),
    date: d2,
    odometer: 10200,
    gallons: 10,
    totalCost: 40,
    pricePerGallon: null,
    calculatedField: 'pricePerGallon',
    isPartialFillUp: false,
    notes: null,
  })

  const toEdit = await recordFuelLogViaApi(page, vehicleId, {
    commandId: randomUUID(),
    date: d2,
    odometer: 10100,
    gallons: 10,
    totalCost: 40,
    pricePerGallon: null,
    calculatedField: 'pricePerGallon',
    isPartialFillUp: false,
    notes: 'seed',
  })

  await page.goto(`/vehicles/${vehicleId}`)
  await expect(page.getByRole('heading', { name: vehicleName })).toBeVisible()

  const history = page.getByLabel('Fuel log history')
  await expect(history).toBeVisible()

  const cards = history.locator('article')
  await expect(cards).toHaveCount(3)

  // Same-date tie should initially show 10,200 mi before 10,100 mi.
  await expect(cards.first()).toContainText('10,200 mi')

  // Open actions for the log we intend to edit (target via odometer text for stability).
  const targetCard = history.locator('article', { hasText: '10,100 mi' })
  await targetCard.getByLabel('Fuel log actions').click()
  await targetCard.getByRole('button', { name: /^edit$/i }).click()

  await expect(page.getByRole('heading', { name: 'Edit Fill-up' })).toBeVisible()

  // Pre-populated fields (including calc labeling on the calculated field).
  await expect(page.getByLabel('Date')).toHaveValue(d2)
  await expect(page.getByLabel('Odometer')).toHaveValue('10100')
  await expect(page.getByLabel('Gallons')).toHaveValue('10')
  await expect(page.getByLabel('Total Cost')).toHaveValue('40')

  const price = page.getByLabel('Price/gal')
  await expect(price).not.toHaveValue('')
  await expect(page.getByText(/^calc$/i)).toBeVisible()

  // Edit odometer so the log should jump above the 10,200 entry for the same-date tie.
  await page.getByLabel('Odometer').fill('10300')
  await page.getByLabel('Notes').fill('edited')

  await page.getByRole('button', { name: 'Save Changes' }).click()
  await expect(page.getByText('Changes saved')).toBeVisible()

  await page.waitForURL(`/vehicles/${vehicleId}`)

  // List refresh should pull server-sorted order: 10,300 mi should now be first.
  await expect(history.locator('article').first()).toContainText('10,300 mi')

  // Notes pill should show for the edited log.
  await expect(history.locator('article').first()).toContainText(/^notes$/im)

  // Sanity: edit route is correct and ID remains stable (API returned a fuelLogId).
  expect(toEdit.fuelLogId).toMatch(/[0-9a-fA-F-]{36}/)
})

test('Delete: requires confirmation dialog; confirm deletes and MPG badge updates from server recalculation', async ({
  browser,
}) => {
  const { page } = await newAuthedPage({ browser })

  const vehicleName = `TASK-006 Delete Fuel ${Date.now()}`
  const vehicleId = await createVehicleViaApi(page, vehicleName)

  const today = new Date()
  const d1 = yyyyMmDd(subtractDays(today, 30))
  const d2 = yyyyMmDd(subtractDays(today, 15))
  const d3 = yyyyMmDd(subtractDays(today, 1))

  // Seed 3 full fill-ups with consistent gallons so MPG changes predictably when the middle entry is deleted:
  // - Before delete: latest MPG uses delta from middle
  // - After delete: latest MPG recalculates using delta from oldest
  await recordFuelLogViaApi(page, vehicleId, {
    commandId: randomUUID(),
    date: d1,
    odometer: 10000,
    gallons: 10,
    totalCost: 40,
    pricePerGallon: null,
    calculatedField: 'pricePerGallon',
    isPartialFillUp: false,
    notes: null,
  })

  const middle = await recordFuelLogViaApi(page, vehicleId, {
    commandId: randomUUID(),
    date: d2,
    odometer: 10200,
    gallons: 10,
    totalCost: 40,
    pricePerGallon: null,
    calculatedField: 'pricePerGallon',
    isPartialFillUp: false,
    notes: null,
  })

  await recordFuelLogViaApi(page, vehicleId, {
    commandId: randomUUID(),
    date: d3,
    odometer: 10400,
    gallons: 10,
    totalCost: 40,
    pricePerGallon: null,
    calculatedField: 'pricePerGallon',
    isPartialFillUp: false,
    notes: null,
  })

  await page.goto(`/vehicles/${vehicleId}`)
  await expect(page.getByRole('heading', { name: vehicleName })).toBeVisible()

  const history = page.getByLabel('Fuel log history')
  await expect(history).toBeVisible()

  // Capture latest mpg before delete.
  const mpgBefore = await history.locator('article').first().locator('text=/\\bmpg\\b/i').textContent()

  const middleCard = history.locator('article', { hasText: '10,200 mi' })
  await middleCard.getByLabel('Fuel log actions').click()
  await middleCard.getByRole('button', { name: /^delete$/i }).click()

  const dialog = page.getByRole('dialog')
  await expect(dialog).toBeVisible()
  await expect(dialog).toContainText('Delete this fill-up from')

  // Cancel should keep entry.
  await dialog.getByRole('button', { name: /^cancel$/i }).click()
  await expect(dialog).toBeHidden()
  await expect(page.getByText('10,200 mi')).toBeVisible()

  // Delete for real.
  await middleCard.getByLabel('Fuel log actions').click()
  await middleCard.getByRole('button', { name: /^delete$/i }).click()
  await expect(page.getByRole('dialog')).toBeVisible()
  await page.getByRole('dialog').getByRole('button', { name: /^delete$/i }).click()

  await expect(page.getByText('Fill-up deleted')).toBeVisible()

  // Entry removed.
  await expect(page.getByText('10,200 mi')).toHaveCount(0)

  // MPG badge should reflect server recalculation for the latest card.
  const mpgAfter = await history.locator('article').first().locator('text=/\\bmpg\\b/i').textContent()
  expect(mpgAfter).not.toEqual(mpgBefore)

  expect(middle.fuelLogId).toMatch(/[0-9a-fA-F-]{36}/)
})
