import { expect, test } from '@playwright/test'
import { randomUUID } from 'node:crypto'

/**
 * TASK-006 E2E — Maintenance flow
 * Add item → log service → verify the "Last performed" line updates.
 *
 * Targets the *real* running stack (Gateway + Web). No mocking.
 *
 * Auth strategy:
 * - Uses the Gateway test-login endpoint to create an isolated user without WorkOS credentials.
 *
 * Contract source of truth: docs/api/maintenance-tracking.md
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

function formatLocalDate(localDate: string): string {
  const [y, m, d] = localDate.split('-').map((p) => Number.parseInt(p, 10))
  const date = new Date(y, m - 1, d)
  return new Intl.DateTimeFormat('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  }).format(date)
}

function todayLocalDate(): string {
  const d = new Date()
  const y = d.getFullYear()
  const m = `${d.getMonth() + 1}`.padStart(2, '0')
  const day = `${d.getDate()}`.padStart(2, '0')
  return `${y}-${m}-${day}`
}

test('user can add a maintenance item, log service, and see last performed update', async ({
  browser,
}) => {
  const { page } = await newAuthedPage({ browser })

  const vehicleName = `TASK-006 Maint ${Date.now()}`
  const vehicleId = await createVehicleViaApi(page, vehicleName)

  await page.goto(`/vehicles/${vehicleId}`)
  await expect(page.getByRole('heading', { name: vehicleName })).toBeVisible()

  // Open Maintenance tab.
  await page.getByRole('tab', { name: 'Maintenance' }).click()

  // Wait for the loaded state (Add Item button only exists once actionable).
  await expect(page.getByRole('button', { name: 'Add Item' })).toBeEnabled()

  // Add a new item via UI.
  const itemName = `Oil Change ${Date.now()}`
  await page.getByRole('button', { name: 'Add Item' }).click()
  await expect(page.getByRole('heading', { name: 'Add Maintenance Item' })).toBeVisible()

  await page.getByLabel('Name').fill(itemName)
  await page.getByLabel('Mileage Interval').fill('5000')

  await page.getByRole('button', { name: 'Save' }).click()
  await page.waitForURL((url) => url.pathname === `/vehicles/${vehicleId}`)

  // Re-open Maintenance tab and locate the new item.
  await page.getByRole('tab', { name: 'Maintenance' }).click()

  const items = page.getByLabel('Maintenance items')
  await expect(items).toBeVisible()

  const itemCard = items.locator('article', { hasText: itemName })
  await expect(itemCard).toBeVisible()
  await expect(itemCard).toContainText('Last performed: Never performed')

  // Log service for that item.
  await itemCard.getByRole('button', { name: 'Log Service' }).click()
  await expect(page.getByRole('heading', { name: 'Log Service' })).toBeVisible()

  const serviceDate = todayLocalDate()
  const formattedDate = formatLocalDate(serviceDate)
  const odometer = 12345

  await page.getByLabel('Date').fill(serviceDate)
  await page.getByLabel('Odometer').fill(`${odometer}`)

  await page.getByRole('button', { name: 'Save' }).click()
  await page.waitForURL((url) => url.pathname === `/vehicles/${vehicleId}`)

  // Verify the "Last performed" line now reflects the new record.
  await page.getByRole('tab', { name: 'Maintenance' }).click()

  const itemCardAfter = page
    .getByLabel('Maintenance items')
    .locator('article', { hasText: itemName })

  await expect(itemCardAfter).toBeVisible()

  // UI formats date and odometer; be tolerant of locale commas.
  await expect(itemCardAfter).toContainText(new RegExp(`Last performed: ${formattedDate}`))
  await expect(itemCardAfter).toContainText(/12,?345 mi/)
})
