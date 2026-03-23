import { expect, test } from '@playwright/test'
import { randomUUID } from 'node:crypto'

/**
 * TASK-005 E2E — Maintenance tab accordion + add/edit item + log/edit/delete service
 *
 * Targets the *real* running stack (Gateway + Web). No mocking.
 * Auth strategy: uses the Gateway test-login endpoint to create an isolated user per test.
 * Contract source of truth: docs/api/maintenance-tracking.md
 */

async function newAuthedPage({ browser }: { browser: any }) {
  const context = await browser.newContext()
  const page = await context.newPage()

  const userId = `pw-task005-mnt-${Date.now()}-${Math.random().toString(16).slice(2)}`
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

async function defineMaintenanceItemViaApi(page: any, vehicleId: string, name: string) {
  const resp = await page.request.post(`/api/v1/garage/vehicles/${vehicleId}/maintenance/items`, {
    data: {
      commandId: randomUUID(),
      name,
      mileageInterval: 5000,
      timeIntervalMonths: null,
      // leave warnings omitted to exercise server defaults
      initialLastPerformedDate: null,
      initialLastPerformedOdometer: null,
    },
  })

  expect([200, 201]).toContain(resp.status())
  const body = (await resp.json()) as { itemId: string }
  expect(body.itemId).toMatch(/[0-9a-fA-F-]{36}/)
  return body.itemId
}

async function recordServiceViaApi(page: any, vehicleId: string, itemId: string, date: string, odometer: number) {
  const resp = await page.request.post(
    `/api/v1/garage/vehicles/${vehicleId}/maintenance/items/${itemId}/records`,
    {
      data: {
        commandId: randomUUID(),
        date,
        odometer,
        cost: 0,
        notes: 'seed',
      },
    },
  )

  expect([200, 201]).toContain(resp.status())
  const body = (await resp.json()) as { recordId: string }
  expect(body.recordId).toMatch(/[0-9a-fA-F-]{36}/)
  return body.recordId
}

test('Maintenance: user can add an item via UI (requires interval) and see it on the tab', async ({
  browser,
}) => {
  const { page } = await newAuthedPage({ browser })

  const vehicleName = `TASK-005 Maintenance Add ${Date.now()}`
  const vehicleId = await createVehicleViaApi(page, vehicleName)

  await page.goto(`/vehicles/${vehicleId}`)
  await expect(page.getByRole('heading', { name: vehicleName })).toBeVisible()

  await page.getByRole('tab', { name: 'Maintenance' }).click()

  await expect(page.getByRole('button', { name: 'Add Item' })).toBeVisible()
  await page.getByRole('button', { name: 'Add Item' }).click()

  await expect(page.getByRole('heading', { name: 'Add Maintenance Item' })).toBeVisible()
  await expect(page.getByText(/starting point/i)).toBeVisible()

  const itemName = `Oil Change ${Date.now()}`
  await page.getByLabel('Name').fill(itemName)
  await page.getByLabel('Mileage Interval').fill('5000')

  await page.getByRole('button', { name: /^save$/i }).click()

  await expect(page.getByText(`${itemName} added`)).toBeVisible()

  // Back on vehicle; re-open Maintenance and confirm the item renders.
  await page.waitForURL(`/vehicles/${vehicleId}`)
  await page.getByRole('tab', { name: 'Maintenance' }).click()
  await expect(page.getByText(itemName)).toBeVisible()
})

test('Maintenance: user can edit an item via UI', async ({ browser }) => {
  const { page } = await newAuthedPage({ browser })

  const vehicleName = `TASK-005 Maintenance Edit ${Date.now()}`
  const vehicleId = await createVehicleViaApi(page, vehicleName)

  const itemName = `Cabin Filter ${Date.now()}`
  await defineMaintenanceItemViaApi(page, vehicleId, itemName)

  await page.goto(`/vehicles/${vehicleId}`)
  await expect(page.getByRole('heading', { name: vehicleName })).toBeVisible()

  await page.getByRole('tab', { name: 'Maintenance' }).click()
  await expect(page.getByText(itemName)).toBeVisible()

  // Expand and navigate to edit.
  await page.getByText(itemName).click()
  await page.getByRole('button', { name: 'Edit Item' }).click()

  await expect(page.getByRole('heading', { name: 'Edit Maintenance Item' })).toBeVisible()

  const updated = `${itemName} (Updated)`
  await page.getByLabel('Name').fill(updated)
  await page.getByRole('button', { name: /^save$/i }).click()

  await expect(page.getByText('Changes saved')).toBeVisible()
  await page.waitForURL(`/vehicles/${vehicleId}`)

  await page.getByRole('tab', { name: 'Maintenance' }).click()
  await expect(page.getByText(updated)).toBeVisible()
})

test('Maintenance: user can log service, delete the record (confirmation), and remove the item (confirmation)', async ({
  browser,
}) => {
  const { page } = await newAuthedPage({ browser })

  const vehicleName = `TASK-005 Maintenance CRUD ${Date.now()}`
  const vehicleId = await createVehicleViaApi(page, vehicleName)

  const itemName = `Brake Fluid ${Date.now()}`
  const itemId = await defineMaintenanceItemViaApi(page, vehicleId, itemName)

  // Seed an initial record so delete confirmation has something to operate on.
  await recordServiceViaApi(page, vehicleId, itemId, '2026-03-01', 12000)

  await page.goto(`/vehicles/${vehicleId}`)
  await page.getByRole('tab', { name: 'Maintenance' }).click()

  await expect(page.getByText(itemName)).toBeVisible()

  const itemCard = page.locator('article', { hasText: itemName })

  // Log a new service via UI.
  await itemCard.getByRole('button', { name: /log service/i }).click()
  await expect(page.getByRole('heading', { name: 'Log Service' })).toBeVisible()

  await page.getByLabel('Date').fill('2026-03-23')
  await page.getByLabel('Odometer').fill('12500')
  await page.getByLabel('Cost').fill('80')

  await page.getByRole('button', { name: /^save$/i }).click()
  await expect(page.getByText('Service recorded!')).toBeVisible()

  await page.waitForURL(`/vehicles/${vehicleId}`)
  await page.getByRole('tab', { name: 'Maintenance' }).click()

  // Expand to view service history.
  await page.getByText(itemName).click()

  // Delete one record via overflow menu + confirmation.
  const history = page.getByLabel('Service history')
  await expect(history).toBeVisible()

  const someRecord = history.locator('div', { hasText: 'Mar' }).first()
  await someRecord.getByLabel('Service record actions').click()
  await someRecord.getByRole('button', { name: /^delete$/i }).click()

  const dialog = page.getByRole('dialog')
  await expect(dialog).toBeVisible()
  await expect(dialog).toContainText('Delete this service record from')

  // Cancel first.
  await dialog.getByRole('button', { name: /^cancel$/i }).click()
  await expect(dialog).toBeHidden()

  // Delete for real.
  await someRecord.getByLabel('Service record actions').click()
  await someRecord.getByRole('button', { name: /^delete$/i }).click()
  await expect(page.getByRole('dialog')).toBeVisible()
  await page.getByRole('dialog').getByRole('button', { name: /^delete$/i }).click()

  await expect(page.getByText('Service record deleted')).toBeVisible()

  // Remove the item via confirmation.
  await page.getByRole('button', { name: 'Remove Item' }).click()

  const removeDialog = page.getByRole('dialog')
  await expect(removeDialog).toBeVisible()
  await expect(removeDialog).toContainText('Remove')

  await removeDialog.getByRole('button', { name: /^cancel$/i }).click()
  await expect(removeDialog).toBeHidden()

  await page.getByRole('button', { name: 'Remove Item' }).click()
  await expect(page.getByRole('dialog')).toBeVisible()
  await page.getByRole('dialog').getByRole('button', { name: /^remove$/i }).click()

  await expect(page.getByText(`${itemName} removed`)).toBeVisible()
  await expect(page.getByText(itemName)).toHaveCount(0)
})
