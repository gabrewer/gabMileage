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

describe('TASK-006 Fuel log edit/delete flows', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn())
  })

  it('Edit: selecting Edit opens the Edit Fill-up page with all fields pre-populated (including calc labeling) and Save Changes', async () => {
    const vehicleId = '2a8d67b1-1c07-4c9b-a7f1-f9f3d3a2b2df'
    const fuelLogId = 'b5c4c1a6-9cc0-48c5-8a21-45b1b4f6c4b6'

    const fetchMock = vi.mocked(fetch)

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

      if (url.endsWith(`/api/v1/garage/vehicles/${vehicleId}/fuel-logs`) && (!init || init.method === 'GET')) {
        return jsonResponse(
          {
            vehicleId,
            lastOdometer: 45230,
            logs: [
              {
                fuelLogId,
                date: '2026-03-15',
                odometer: 45230,
                gallons: 12.5,
                totalCost: 42.5,
                pricePerGallon: 3.4,
                calculatedField: 'pricePerGallon',
                isPartialFillUp: false,
                notes: 'Regular unleaded',
                mpg: 28.4,
                createdAt: '2026-03-15T14:30:00Z',
              },
            ],
          },
          { status: 200 },
        )
      }

      // Edit flow should save via PUT.
      if (
        url.endsWith(`/api/v1/garage/vehicles/${vehicleId}/fuel-logs/${fuelLogId}`) &&
        init?.method === 'PUT'
      ) {
        return jsonResponse({ fuelLogId }, { status: 200 })
      }

      throw new Error(`Unexpected fetch: ${url} (${init?.method ?? 'GET'})`)
    })

    await renderAt(`/vehicles/${vehicleId}`)

    const user = userEvent.setup()

    const card = screen.getByText('Mar 15, 2026').closest('article')
    expect(card).not.toBeNull()

    await user.click(within(card!).getByLabelText('Fuel log actions'))
    await user.click(within(card!).getByRole('button', { name: /^edit$/i }))

    // Must navigate to the edit page.
    expect(await screen.findByRole('heading', { name: 'Edit Fill-up' })).toBeInTheDocument()

    // All fields must be pre-populated.
    expect(screen.getByLabelText(/^date$/i)).toHaveValue('2026-03-15')
    expect(screen.getByLabelText(/odometer/i)).toHaveValue('45230')

    expect(screen.getByLabelText(/^gallons$/i)).toHaveValue('12.5')
    expect(screen.getByLabelText(/total\s*cost/i)).toHaveValue('42.5')
    expect(screen.getByLabelText(/price.*gal/i)).toHaveValue('3.4')

    expect(screen.getByLabelText(/partial/i)).not.toBeChecked()
    expect(screen.getByLabelText(/notes/i)).toHaveValue('Regular unleaded')

    // Calculated field must be labeled.
    const price = screen.getByLabelText(/price.*gal/i)
    expect(within(price.parentElement!).getByText(/^calc$/i)).toBeInTheDocument()

    // Save button copy must match the sprint brief.
    expect(screen.getByRole('button', { name: 'Save Changes' })).toBeEnabled()
  })

  it('Delete: requires a confirmation dialog with exact copy; confirm deletes and refreshes list/MPG badges from server', async () => {
    const vehicleId = '2a8d67b1-1c07-4c9b-a7f1-f9f3d3a2b2df'
    const toDeleteId = 'log-to-delete'

    const fetchMock = vi.mocked(fetch)

    let deleted = false

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

      if (url.endsWith(`/api/v1/garage/vehicles/${vehicleId}/fuel-logs`) && (!init || init.method === 'GET')) {
        if (!deleted) {
          return jsonResponse(
            {
              vehicleId,
              lastOdometer: 45650,
              logs: [
                {
                  fuelLogId: 'latest',
                  date: '2026-03-29',
                  odometer: 45650,
                  gallons: 10,
                  totalCost: 35,
                  pricePerGallon: 3.5,
                  calculatedField: null,
                  isPartialFillUp: false,
                  notes: null,
                  mpg: 42.0,
                  createdAt: '2026-03-29T12:00:00Z',
                },
                {
                  fuelLogId: toDeleteId,
                  date: '2026-03-15',
                  odometer: 45230,
                  gallons: 12.5,
                  totalCost: 42.5,
                  pricePerGallon: 3.4,
                  calculatedField: 'pricePerGallon',
                  isPartialFillUp: false,
                  notes: null,
                  mpg: 28.4,
                  createdAt: '2026-03-15T14:30:00Z',
                },
                {
                  fuelLogId: 'older',
                  date: '2026-03-01',
                  odometer: 44800,
                  gallons: 11,
                  totalCost: 35.2,
                  pricePerGallon: 3.2,
                  calculatedField: null,
                  isPartialFillUp: false,
                  notes: null,
                  mpg: null,
                  createdAt: '2026-03-01T14:30:00Z',
                },
              ],
            },
            { status: 200 },
          )
        }

        // After delete, server recalculates MPG; UI must reflect server values.
        return jsonResponse(
          {
            vehicleId,
            lastOdometer: 45650,
            logs: [
              {
                fuelLogId: 'latest',
                date: '2026-03-29',
                odometer: 45650,
                gallons: 10,
                totalCost: 35,
                pricePerGallon: 3.5,
                calculatedField: null,
                isPartialFillUp: false,
                notes: null,
                mpg: 38.0,
                createdAt: '2026-03-29T12:00:00Z',
              },
              {
                fuelLogId: 'older',
                date: '2026-03-01',
                odometer: 44800,
                gallons: 11,
                totalCost: 35.2,
                pricePerGallon: 3.2,
                calculatedField: null,
                isPartialFillUp: false,
                notes: null,
                mpg: null,
                createdAt: '2026-03-01T14:30:00Z',
              },
            ],
          },
          { status: 200 },
        )
      }

      if (
        url.endsWith(`/api/v1/garage/vehicles/${vehicleId}/fuel-logs/${toDeleteId}`) &&
        init?.method === 'DELETE'
      ) {
        deleted = true
        return new Response(null, { status: 204 })
      }

      throw new Error(`Unexpected fetch: ${url} (${init?.method ?? 'GET'})`)
    })

    await renderAt(`/vehicles/${vehicleId}`)

    const user = userEvent.setup()

    // Baseline: both mpg badges from the initial list should be visible.
    expect(await screen.findByText('42.0 mpg')).toBeInTheDocument()
    expect(screen.getByText('28.4 mpg')).toBeInTheDocument()

    const card = screen.getByText('Mar 15, 2026').closest('article')
    expect(card).not.toBeNull()

    await user.click(within(card!).getByLabelText('Fuel log actions'))
    await user.click(within(card!).getByRole('button', { name: /^delete$/i }))

    // Must be a confirmation dialog (not an undo toast) with exact copy from the sprint brief.
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText('Delete this fill-up from Mar 15, 2026?')).toBeInTheDocument()

    // Buttons: Delete (destructive) + Cancel.
    await user.click(within(dialog).getByRole('button', { name: /^delete$/i }))

    // Entry removed from list.
    expect(await screen.findByText('Fill-up deleted')).toBeInTheDocument()
    expect(screen.queryByText('Mar 15, 2026')).not.toBeInTheDocument()

    // MPG badges must reflect server recalculation.
    expect(await screen.findByText('38.0 mpg')).toBeInTheDocument()
    expect(screen.queryByText('42.0 mpg')).not.toBeInTheDocument()
  })

  it('Fuel list sorting: for same-date ties, logs must sort by odometer desc (not createdAt), so edits/deletes re-render correctly', async () => {
    const vehicleId = '2a8d67b1-1c07-4c9b-a7f1-f9f3d3a2b2df'

    const fetchMock = vi.mocked(fetch)

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

      if (url.endsWith(`/api/v1/garage/vehicles/${vehicleId}/fuel-logs`) && (!init || init.method === 'GET')) {
        // Intentionally return out of display order for a same-date tie.
        return jsonResponse(
          {
            vehicleId,
            lastOdometer: 45230,
            logs: [
              {
                fuelLogId: 'same-date-lower-odo',
                date: '2026-03-15',
                odometer: 45100,
                gallons: 10,
                totalCost: 30,
                pricePerGallon: 3,
                calculatedField: null,
                isPartialFillUp: false,
                notes: null,
                mpg: 30.0,
                // Newer createdAt, but lower odometer: correct tie-breaker must still prefer higher odometer.
                createdAt: '2026-03-16T10:00:00Z',
              },
              {
                fuelLogId: 'same-date-higher-odo',
                date: '2026-03-15',
                odometer: 45230,
                gallons: 12,
                totalCost: 36,
                pricePerGallon: 3,
                calculatedField: null,
                isPartialFillUp: false,
                notes: null,
                mpg: 28.0,
                createdAt: '2026-03-16T09:00:00Z',
              },
              {
                fuelLogId: 'older',
                date: '2026-03-01',
                odometer: 44800,
                gallons: 11,
                totalCost: 35.2,
                pricePerGallon: 3.2,
                calculatedField: null,
                isPartialFillUp: false,
                notes: null,
                mpg: null,
                createdAt: '2026-03-01T14:30:00Z',
              },
            ],
          },
          { status: 200 },
        )
      }

      throw new Error(`Unexpected fetch: ${url} (${init?.method ?? 'GET'})`)
    })

    await renderAt(`/vehicles/${vehicleId}`)

    const history = await screen.findByRole('region', { name: /fuel log history/i })
    const cards = Array.from(history.querySelectorAll('article'))
    expect(cards.length).toBeGreaterThanOrEqual(2)

    // Same-date tie-breaker must prefer higher odometer first (45,230 before 45,100).
    expect(cards[0]?.textContent ?? '').toContain('45,230 mi')
    expect(cards[1]?.textContent ?? '').toContain('45,100 mi')
  })
})
