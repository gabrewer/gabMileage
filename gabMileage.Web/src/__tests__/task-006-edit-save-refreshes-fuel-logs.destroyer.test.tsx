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

describe('TASK-006 Destroyer — edit should refresh fuel list after save', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn())
  })

  it('After editing a prior fill-up, returning to the vehicle page must re-fetch fuel logs so MPG badges reflect server recalculation (not stale loader data)', async () => {
    const vehicleId = '2a8d67b1-1c07-4c9b-a7f1-f9f3d3a2b2df'
    const fuelLogIdToEdit = 'middle'

    const fetchMock = vi.mocked(fetch)

    let updated = false

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
        // The key assertion: the UI must re-fetch this endpoint after edit.
        // If it doesn't, the page will keep the initial MPG badge even though the server would
        // recalculate MPG for subsequent full fill-ups.
        if (!updated) {
          return jsonResponse(
            {
              vehicleId,
              lastOdometer: 10400,
              logs: [
                {
                  fuelLogId: 'latest',
                  date: '2026-03-29',
                  odometer: 10400,
                  gallons: 10,
                  totalCost: 40,
                  pricePerGallon: 4,
                  calculatedField: null,
                  isPartialFillUp: false,
                  notes: null,
                  mpg: 42.0,
                  createdAt: '2026-03-29T12:00:00Z',
                },
                {
                  fuelLogId: fuelLogIdToEdit,
                  date: '2026-03-15',
                  odometer: 10200,
                  gallons: 10,
                  totalCost: 40,
                  pricePerGallon: 4,
                  calculatedField: null,
                  isPartialFillUp: false,
                  notes: null,
                  mpg: 20.0,
                  createdAt: '2026-03-15T14:30:00Z',
                },
                {
                  fuelLogId: 'older',
                  date: '2026-03-01',
                  odometer: 10000,
                  gallons: 10,
                  totalCost: 40,
                  pricePerGallon: 4,
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

        // After edit, server recalculates MPG for the latest entry.
        return jsonResponse(
          {
            vehicleId,
            lastOdometer: 10400,
            logs: [
              {
                fuelLogId: 'latest',
                date: '2026-03-29',
                odometer: 10400,
                gallons: 10,
                totalCost: 40,
                pricePerGallon: 4,
                calculatedField: null,
                isPartialFillUp: false,
                notes: null,
                mpg: 38.0,
                createdAt: '2026-03-29T12:00:00Z',
              },
              {
                fuelLogId: fuelLogIdToEdit,
                date: '2026-03-15',
                odometer: 10100,
                gallons: 10,
                totalCost: 40,
                pricePerGallon: 4,
                calculatedField: null,
                isPartialFillUp: false,
                notes: null,
                mpg: 20.0,
                createdAt: '2026-03-15T14:30:00Z',
              },
              {
                fuelLogId: 'older',
                date: '2026-03-01',
                odometer: 10000,
                gallons: 10,
                totalCost: 40,
                pricePerGallon: 4,
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
        url.endsWith(`/api/v1/garage/vehicles/${vehicleId}/fuel-logs/${fuelLogIdToEdit}`) &&
        init?.method === 'PUT'
      ) {
        updated = true
        return jsonResponse({ fuelLogId: fuelLogIdToEdit }, { status: 200 })
      }

      throw new Error(`Unexpected fetch: ${url} (${init?.method ?? 'GET'})`)
    })

    await renderAt(`/vehicles/${vehicleId}`)

    const user = userEvent.setup()

    // Baseline mpg from initial list.
    expect(await screen.findByText('42.0 mpg')).toBeInTheDocument()

    const card = screen.getByText('Mar 15, 2026').closest('article')
    expect(card).not.toBeNull()

    await user.click(within(card!).getByLabelText('Fuel log actions'))
    await user.click(within(card!).getByRole('button', { name: /^edit$/i }))

    expect(await screen.findByRole('heading', { name: 'Edit Fill-up' })).toBeInTheDocument()

    // Change the middle odometer so server would recalculate MPG for later entries.
    await user.clear(screen.getByLabelText(/odometer/i))
    await user.type(screen.getByLabelText(/odometer/i), '10100')

    await user.click(screen.getByRole('button', { name: 'Save Changes' }))

    expect(await screen.findByText('Changes saved')).toBeInTheDocument()

    // Must re-fetch and reflect server-updated MPG.
    expect(await screen.findByText('38.0 mpg')).toBeInTheDocument()

    // Defensive: ensure we actually performed a second GET /fuel-logs.
    const fuelGets = fetchMock.mock.calls.filter(([req, init]) => {
      const url = urlOf(req)
      return url.endsWith(`/api/v1/garage/vehicles/${vehicleId}/fuel-logs`) && (!init || init.method === 'GET')
    }).length

    expect(fuelGets).toBeGreaterThanOrEqual(2)
  })
})
