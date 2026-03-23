import React from 'react'
import {
  RouterProvider,
  createMemoryHistory,
  createRouter,
} from '@tanstack/react-router'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { routeTree } from '../routeTree.gen'

async function renderAt(path: string) {
  const router = createRouter({
    routeTree,
    history: createMemoryHistory({
      initialEntries: [path],
    }),
  })

  render(<RouterProvider router={router} />)

  // Ensure initial route matches/loaders have a chance to run before assertions.
  await router.load()

  return router
}

function jsonResponse(body: unknown, init?: ResponseInit) {
  return new Response(JSON.stringify(body), {
    ...init,
    headers: {
      'content-type': 'application/json',
      ...(init?.headers ?? {}),
    },
  })
}

function urlOf(input: RequestInfo | URL) {
  if (typeof input === 'string') return input
  if (input instanceof URL) return input.toString()
  return input.url
}

function expectBefore(a: HTMLElement, b: HTMLElement) {
  // a should appear earlier in DOM order than b
  const pos = a.compareDocumentPosition(b)
  expect(pos & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
}

describe('TASK-005 Maintenance UI (accordion + add/edit/log)', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn())
  })

  it('Maintenance tab renders an urgency-sorted accordion (overdue → dueSoon → ok; then alphabetical)', async () => {
    const vehicleId = '2a8d67b1-1c07-4c9b-a7f1-f9f3d3a2b2df'
    const fetchMock = vi.mocked(fetch)

    // Intentionally return items in the WRONG order to ensure the UI sorts them.
    const maintenanceResponse = {
      vehicleId,
      lastOdometer: 49600,
      items: [
        {
          itemId: 'i-ok-oil',
          name: 'Oil Change',
          mileageInterval: 5000,
          timeIntervalMonths: 6,
          mileageWarning: 500,
          timeWarningDays: 14,
          status: 'ok',
          statusDetail: 'Next due in ~2,300 mi',
          lastPerformedDate: '2026-01-15',
          lastPerformedOdometer: 45230,
          records: [],
          createdAt: '2026-03-23T02:00:00Z',
        },
        {
          itemId: 'i-due-cabin',
          name: 'Cabin Filter',
          mileageInterval: 12000,
          timeIntervalMonths: null,
          mileageWarning: 500,
          timeWarningDays: 14,
          status: 'dueSoon',
          statusDetail: 'Due in ~450 mi',
          lastPerformedDate: '2025-12-01',
          lastPerformedOdometer: 38000,
          records: [],
          createdAt: '2026-03-23T02:00:00Z',
        },
        {
          itemId: 'i-overdue-brake',
          name: 'Brake Fluid',
          mileageInterval: null,
          timeIntervalMonths: 24,
          mileageWarning: 500,
          timeWarningDays: 14,
          status: 'overdue',
          statusDetail: 'Overdue by 3 weeks',
          lastPerformedDate: '2024-01-10',
          lastPerformedOdometer: 25000,
          records: [],
          createdAt: '2026-03-23T02:00:00Z',
        },
        {
          itemId: 'i-due-air',
          name: 'Air Filter',
          mileageInterval: 12000,
          timeIntervalMonths: null,
          mileageWarning: 500,
          timeWarningDays: 14,
          status: 'dueSoon',
          statusDetail: 'Due in ~900 mi',
          lastPerformedDate: '2025-12-15',
          lastPerformedOdometer: 39000,
          records: [],
          createdAt: '2026-03-23T02:00:00Z',
        },
      ],
    }

    fetchMock.mockImplementation(async (input, init) => {
      const url = urlOf(input)

      if (url.endsWith('/api/v1/me')) {
        return jsonResponse(
          {
            userId: 'user-123',
            displayName: 'Gab',
            email: 'gab@example.com',
            avatarUrl: null,
            createdAt: '2026-03-22T23:26:39Z',
          },
          { status: 200 },
        )
      }

      if (url.endsWith(`/api/v1/garage/vehicles/${vehicleId}`) && (!init || init.method === 'GET')) {
        return jsonResponse(
          {
            vehicleId,
            name: 'Blue Thunder',
            make: null,
            model: null,
            year: null,
            photoUrl: null,
            photoThumbnailUrl: null,
            createdAt: '2026-03-23T01:45:00Z',
            updatedAt: '2026-03-23T01:45:00Z',
          },
          { status: 200 },
        )
      }

      // Vehicle page loader currently always requests fuel logs.
      if (url.endsWith(`/api/v1/garage/vehicles/${vehicleId}/fuel-logs`) && (!init || init.method === 'GET')) {
        return jsonResponse({ vehicleId, logs: [], lastOdometer: 49600 }, { status: 200 })
      }

      // New Maintenance tab query endpoint (Sprint 04 contract)
      if (url.endsWith(`/api/v1/garage/vehicles/${vehicleId}/maintenance`) && (!init || init.method === 'GET')) {
        return jsonResponse(maintenanceResponse, { status: 200 })
      }

      throw new Error(`Unexpected fetch: ${url} (${init?.method ?? 'GET'})`)
    })

    await renderAt(`/vehicles/${vehicleId}`)

    const user = userEvent.setup()
    await user.click(screen.getByRole('tab', { name: 'Maintenance' }))

    // Once implemented, the tab should show these items (not the placeholder).
    const brake = await screen.findByText('Brake Fluid')
    const air = screen.getByText('Air Filter')
    const cabin = screen.getByText('Cabin Filter')
    const oil = screen.getByText('Oil Change')

    // Group ordering: overdue first, then dueSoon, then ok.
    expectBefore(brake, air)
    expectBefore(brake, cabin)
    expectBefore(air, oil)
    expectBefore(cabin, oil)

    // Alphabetical within dueSoon: Air Filter should come before Cabin Filter.
    expectBefore(air, cabin)

    // Each card should show the server-provided statusDetail and a last performed line.
    const brakeCard = brake.closest('article')
    expect(brakeCard).not.toBeNull()
    expect(within(brakeCard!).getByText('Overdue by 3 weeks')).toBeInTheDocument()
    expect(within(brakeCard!).getByText(/last performed/i)).toBeInTheDocument()

    const oilCard = oil.closest('article')
    expect(oilCard).not.toBeNull()
    expect(within(oilCard!).getByText('Next due in ~2,300 mi')).toBeInTheDocument()
    expect(within(oilCard!).getByText(/last performed/i)).toBeInTheDocument()
  })

  it('Add Maintenance Item: requires at least one interval (mileage/time) and includes a Starting point initial-state section', async () => {
    const vehicleId = '2a8d67b1-1c07-4c9b-a7f1-f9f3d3a2b2df'
    const fetchMock = vi.mocked(fetch)

    fetchMock.mockImplementation(async (input, init) => {
      const url = urlOf(input)

      if (url.endsWith('/api/v1/me')) {
        return jsonResponse({ userId: 'user-123', displayName: 'Gab', email: 'gab@example.com', avatarUrl: null, createdAt: '2026-03-22T23:26:39Z' }, { status: 200 })
      }

      if (url.endsWith(`/api/v1/garage/vehicles/${vehicleId}`) && (!init || init.method === 'GET')) {
        return jsonResponse({ vehicleId, name: 'Blue Thunder', make: null, model: null, year: null, photoUrl: null, photoThumbnailUrl: null, createdAt: '2026-03-23T01:45:00Z', updatedAt: '2026-03-23T01:45:00Z' }, { status: 200 })
      }

      if (url.endsWith(`/api/v1/garage/vehicles/${vehicleId}/fuel-logs`) && (!init || init.method === 'GET')) {
        return jsonResponse({ vehicleId, logs: [], lastOdometer: 49600 }, { status: 200 })
      }

      if (url.endsWith(`/api/v1/garage/vehicles/${vehicleId}/maintenance`) && (!init || init.method === 'GET')) {
        // Empty state should still allow Add Item.
        return jsonResponse({ vehicleId, items: [], lastOdometer: 49600 }, { status: 200 })
      }

      if (url.endsWith(`/api/v1/garage/vehicles/${vehicleId}/maintenance/items`) && init?.method === 'POST') {
        // Simulate server validation failure when intervals are missing.
        return jsonResponse(
          {
            type: 'https://httpstatuses.com/400',
            title: 'Bad Request',
            status: 400,
            detail: 'Validation failed.',
            errors: {
              intervals: ['Set a mileage interval, time interval, or both'],
            },
          },
          { status: 400, headers: { 'content-type': 'application/problem+json' } },
        )
      }

      throw new Error(`Unexpected fetch: ${url} (${init?.method ?? 'GET'})`)
    })

    await renderAt(`/vehicles/${vehicleId}`)

    const user = userEvent.setup()
    await user.click(screen.getByRole('tab', { name: 'Maintenance' }))

    // When implemented, this CTA should be enabled once maintenance loads.
    await user.click(await screen.findByRole('button', { name: 'Add Item' }))

    // Must navigate to the Add Maintenance Item page.
    expect(
      await screen.findByRole('heading', { name: /add maintenance item/i }),
    ).toBeInTheDocument()

    // Add page includes initial-state seeding section.
    expect(screen.getByText(/starting point/i)).toBeInTheDocument()

    // Submit with no intervals should show a stable validation surface.
    await user.type(screen.getByLabelText(/name/i), 'Oil Change')

    await user.click(screen.getByRole('button', { name: /^save$/i }))

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(/interval/i)
  })

  it('Log Service: after saving, returning to the vehicle Maintenance tab refreshes last performed + status detail immediately', async () => {
    const vehicleId = '2a8d67b1-1c07-4c9b-a7f1-f9f3d3a2b2df'
    const itemId = 'f8a7f8b2-3a3f-4b50-93b0-5c8accc7bbf3'

    const fetchMock = vi.mocked(fetch)

    let version: 'before' | 'after' = 'before'

    const before = {
      vehicleId,
      lastOdometer: 49600,
      items: [
        {
          itemId,
          name: 'Oil Change',
          mileageInterval: 5000,
          timeIntervalMonths: 6,
          mileageWarning: 500,
          timeWarningDays: 14,
          status: 'dueSoon',
          statusDetail: 'Due in ~450 mi',
          lastPerformedDate: '2026-01-15',
          lastPerformedOdometer: 45230,
          records: [
            {
              recordId: 'rec-old',
              itemId,
              date: '2026-01-15',
              odometer: 45230,
              cost: 69.99,
              notes: 'Full synthetic',
              createdAt: '2026-01-15T18:00:00Z',
            },
          ],
          createdAt: '2026-03-23T02:00:00Z',
        },
      ],
    }

    const after = {
      ...before,
      items: [
        {
          ...before.items[0],
          status: 'ok',
          statusDetail: 'Next due in ~5,000 mi or 6 months',
          lastPerformedDate: '2026-03-23',
          lastPerformedOdometer: 49600,
          records: [
            {
              recordId: 'rec-new',
              itemId,
              date: '2026-03-23',
              odometer: 49600,
              cost: 80,
              notes: null,
              createdAt: '2026-03-23T10:00:00Z',
            },
            ...before.items[0].records,
          ],
        },
      ],
    }

    fetchMock.mockImplementation(async (input, init) => {
      const url = urlOf(input)

      if (url.endsWith('/api/v1/me')) {
        return jsonResponse({ userId: 'user-123', displayName: 'Gab', email: 'gab@example.com', avatarUrl: null, createdAt: '2026-03-22T23:26:39Z' }, { status: 200 })
      }

      if (url.endsWith(`/api/v1/garage/vehicles/${vehicleId}`) && (!init || init.method === 'GET')) {
        return jsonResponse({ vehicleId, name: 'Blue Thunder', make: null, model: null, year: null, photoUrl: null, photoThumbnailUrl: null, createdAt: '2026-03-23T01:45:00Z', updatedAt: '2026-03-23T01:45:00Z' }, { status: 200 })
      }

      if (url.endsWith(`/api/v1/garage/vehicles/${vehicleId}/fuel-logs`) && (!init || init.method === 'GET')) {
        return jsonResponse({ vehicleId, logs: [], lastOdometer: 49600 }, { status: 200 })
      }

      if (url.endsWith(`/api/v1/garage/vehicles/${vehicleId}/maintenance`) && (!init || init.method === 'GET')) {
        return jsonResponse(version === 'before' ? before : after, { status: 200 })
      }

      if (
        url.endsWith(`/api/v1/garage/vehicles/${vehicleId}/maintenance/items/${itemId}/records`) &&
        init?.method === 'POST'
      ) {
        version = 'after'
        return jsonResponse({ recordId: 'rec-new' }, { status: 201 })
      }

      throw new Error(`Unexpected fetch: ${url} (${init?.method ?? 'GET'})`)
    })

    await renderAt(`/vehicles/${vehicleId}`)

    const user = userEvent.setup()
    await user.click(screen.getByRole('tab', { name: 'Maintenance' }))

    // Baseline render from the initial maintenance read model.
    const oil = await screen.findByText('Oil Change')
    const oilCard = oil.closest('article')
    expect(oilCard).not.toBeNull()
    expect(within(oilCard!).getByText('Due in ~450 mi')).toBeInTheDocument()

    // When implemented, the item card should offer a Log Service action.
    await user.click(within(oilCard!).getByRole('button', { name: /log service/i }))

    expect(
      await screen.findByRole('heading', { name: /log service/i }),
    ).toBeInTheDocument()

    // Fill minimal service record fields.
    await user.clear(screen.getByLabelText(/^date$/i))
    await user.type(screen.getByLabelText(/^date$/i), '2026-03-23')

    await user.type(screen.getByLabelText(/odometer/i), '49600')
    await user.type(screen.getByLabelText(/cost/i), '80')

    await user.click(screen.getByRole('button', { name: /^save$/i }))

    // Returning to the vehicle page and selecting Maintenance should reflect updated last performed/status.
    await user.click(await screen.findByRole('tab', { name: 'Maintenance' }))

    const oilAfter = await screen.findByText('Oil Change')
    const oilAfterCard = oilAfter.closest('article')
    expect(oilAfterCard).not.toBeNull()

    expect(within(oilAfterCard!).getByText('Next due in ~5,000 mi or 6 months')).toBeInTheDocument()
    expect(within(oilAfterCard!).getByText(/last performed/i)).toBeInTheDocument()

    // Strong signal: the Maintenance endpoint should have been queried again after the save.
    const maintenanceGets = (fetchMock as any).mock.calls.filter(([input, init]: any[]) => {
      const u = urlOf(input)
      return u.endsWith(`/api/v1/garage/vehicles/${vehicleId}/maintenance`) && (!init || init.method === 'GET')
    })

    expect(maintenanceGets.length).toBeGreaterThanOrEqual(2)
  })
})
