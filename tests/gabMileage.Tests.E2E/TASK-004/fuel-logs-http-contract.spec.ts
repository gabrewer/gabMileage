import { expect, test } from '@playwright/test'
import { randomUUID } from 'node:crypto'

/**
 * TASK-004 E2E — HTTP API contract for fuel logs (Sprint 03)
 *
 * Targets the *real* running stack (Gateway + Web). No mocking.
 *
 * Contract source of truth: docs/api/fuel-logging.md
 *
 * This spec leans on the existing Gateway test-login endpoint used elsewhere in this repo
 * (see TASK-007) to create isolated users without requiring real WorkOS credentials.
 *
 * Breadcrumbs (frontend-builder)
 * What: Validate fuel log CRUD + ownership scoping using real HTTP calls.
 * Why: The sprint’s acceptance criteria is contract correctness (mpg/calculatedField + 404 scoping).
 * Alternatives considered: UI-driven fuel log flows (rejected: fuel logging UI is not shipped in these tasks yet).
 * Confidence: medium
 */

async function newAuthedPage({ browser }: { browser: any }) {
  const context = await browser.newContext()
  const page = await context.newPage()

  const userId = `pw-task004-${Date.now()}-${Math.random().toString(16).slice(2)}`
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

  // Contract: 201 Created on first commandId, 200 OK on duplicate commandId.
  expect([200, 201]).toContain(resp.status())

  const body = (await resp.json()) as { fuelLogId: string; odometerWarning: string | null }
  expect(body.fuelLogId).toMatch(/[0-9a-fA-F-]{36}/)
  return body
}

test('can record, fetch (with mpg + calculatedField), update, and delete fuel logs', async ({
  browser,
}) => {
  const { page } = await newAuthedPage({ browser })

  const vehicleId = await createVehicleViaApi(
    page,
    `TASK-004 Fuel Vehicle ${Date.now()}-${Math.random().toString(16).slice(2)}`,
  )

  // Record a first fill-up (mpg likely null because there is no prior log).
  const commandId1 = randomUUID()
  const log1 = await recordFuelLogViaApi(page, vehicleId, {
    commandId: commandId1,
    date: '2026-03-15',
    odometer: 10000,
    gallons: 10,
    totalCost: 30,
    pricePerGallon: null,
    calculatedField: 'pricePerGallon',
    isPartialFillUp: false,
    notes: 'E2E seed 1',
  })
  expect(log1.odometerWarning).toBeNull()

  // Record a second fill-up so mpg can be calculated.
  const commandId2 = randomUUID()
  const log2 = await recordFuelLogViaApi(page, vehicleId, {
    commandId: commandId2,
    date: '2026-03-16',
    odometer: 10250,
    gallons: 10,
    totalCost: 30,
    pricePerGallon: null,
    calculatedField: 'pricePerGallon',
    isPartialFillUp: false,
    notes: 'E2E seed 2',
  })

  // Fetch list + assert ordering and derived fields.
  const getResp = await page.request.get(`/api/v1/garage/vehicles/${vehicleId}/fuel-logs`)
  expect(getResp.ok()).toBeTruthy()

  const list = (await getResp.json()) as any
  expect(list.vehicleId).toBe(vehicleId)
  expect(Array.isArray(list.logs)).toBeTruthy()
  expect(list.lastOdometer).toBe(10250)

  const newest = list.logs[0]
  expect(newest.fuelLogId).toBe(log2.fuelLogId)
  expect(newest.calculatedField).toBe('pricePerGallon')
  expect(newest.mpg).not.toBeNull()
  expect(newest.mpg).toBeCloseTo(25.0, 1)

  // Update the newest log: change gallons so mpg changes.
  const updateResp = await page.request.put(
    `/api/v1/garage/vehicles/${vehicleId}/fuel-logs/${log2.fuelLogId}`,
    {
      data: {
        date: '2026-03-16',
        odometer: 10250,
        gallons: 12.5,
        totalCost: 30,
        pricePerGallon: null,
        calculatedField: 'pricePerGallon',
        isPartialFillUp: false,
        notes: 'E2E updated',
      },
    },
  )
  expect(updateResp.ok(), 'Expected PUT to succeed').toBeTruthy()

  const afterUpdate = await page.request.get(`/api/v1/garage/vehicles/${vehicleId}/fuel-logs`)
  const afterUpdateBody = (await afterUpdate.json()) as any
  const updatedNewest = afterUpdateBody.logs[0]
  expect(updatedNewest.fuelLogId).toBe(log2.fuelLogId)
  expect(updatedNewest.mpg).toBeCloseTo(20.0, 1)

  // Delete the newest log and ensure it disappears.
  const deleteResp = await page.request.delete(
    `/api/v1/garage/vehicles/${vehicleId}/fuel-logs/${log2.fuelLogId}`,
  )
  expect(deleteResp.status(), 'Expected DELETE to return 204').toBe(204)

  const afterDelete = await page.request.get(`/api/v1/garage/vehicles/${vehicleId}/fuel-logs`)
  const afterDeleteBody = (await afterDelete.json()) as any
  const ids = (afterDeleteBody.logs as any[]).map((l) => l.fuelLogId)
  expect(ids).toContain(log1.fuelLogId)
  expect(ids).not.toContain(log2.fuelLogId)
  expect(afterDeleteBody.lastOdometer).toBe(10000)
})

test('ownership scoping: other user gets 404 for vehicle and fuel log resources', async ({
  browser,
}) => {
  const { page: user1 } = await newAuthedPage({ browser })
  const { page: user2 } = await newAuthedPage({ browser })

  const vehicleId = await createVehicleViaApi(
    user1,
    `TASK-004 Fuel Ownership ${Date.now()}-${Math.random().toString(16).slice(2)}`,
  )

  const { fuelLogId } = await recordFuelLogViaApi(user1, vehicleId, {
    commandId: randomUUID(),
    date: '2026-03-15',
    odometer: 20000,
    gallons: 10,
    totalCost: 30,
    pricePerGallon: null,
    calculatedField: 'pricePerGallon',
    isPartialFillUp: false,
    notes: null,
  })

  const otherVehicleList = await user2.request.get(
    `/api/v1/garage/vehicles/${vehicleId}/fuel-logs`,
  )
  expect(otherVehicleList.status()).toBe(404)

  const otherFuelLogDelete = await user2.request.delete(
    `/api/v1/garage/vehicles/${vehicleId}/fuel-logs/${fuelLogId}`,
  )
  expect(otherFuelLogDelete.status()).toBe(404)
})
