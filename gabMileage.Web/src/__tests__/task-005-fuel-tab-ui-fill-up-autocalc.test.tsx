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

describe('TASK-005 Fuel tab UI + Log Fill-up auto-calc', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn())
  })

  it('Fuel tab: shows MPG badge only when mpg is non-null (no placeholder when null)', async () => {
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

      if (url.endsWith(`/api/v1/garage/vehicles/${vehicleId}/fuel-logs`)) {
        return jsonResponse(
          {
            vehicleId,
            lastOdometer: 45230,
            logs: [
              {
                fuelLogId: 'b5c4c1a6-9cc0-48c5-8a21-45b1b4f6c4b6',
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
              {
                fuelLogId: 'fba9cb79-14d3-4cbe-8a48-17cb587d89b3',
                date: '2026-03-01',
                odometer: 44800,
                gallons: 11.0,
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

      throw new Error(`Unexpected fetch: ${url}`)
    })

    await renderAt(`/vehicles/${vehicleId}`)

    // Populated fuel tab should show log cards (reverse-chronological list) instead of the empty placeholder.
    expect(await screen.findByText('Mar 15, 2026')).toBeInTheDocument()
    expect(screen.getByText('Mar 1, 2026')).toBeInTheDocument()

    // Acceptance criterion: mpg badge is only rendered when mpg is non-null.
    expect(screen.getByText('28.4 mpg')).toBeInTheDocument()
    expect(screen.queryAllByText(/mpg/i)).toHaveLength(1)
  })

  it('Log Fill-up: auto-calculates on blur (not keystroke) and marks the calculated field with a "calc" pill', async () => {
    const vehicleId = '2a8d67b1-1c07-4c9b-a7f1-f9f3d3a2b2df'
    const fetchMock = vi.mocked(fetch)

    fetchMock.mockImplementation(async (input) => {
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

      if (url.endsWith(`/api/v1/garage/vehicles/${vehicleId}`)) {
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

      if (url.endsWith(`/api/v1/garage/vehicles/${vehicleId}/fuel-logs`)) {
        return jsonResponse(
          {
            vehicleId,
            logs: [],
            lastOdometer: 44800,
          },
          { status: 200 },
        )
      }

      throw new Error(`Unexpected fetch: ${url}`)
    })

    await renderAt(`/vehicles/${vehicleId}`)

    const user = userEvent.setup()

    const logFillUp = await screen.findByRole('button', { name: 'Log Fill-up' })
    expect(logFillUp).toBeEnabled()
    await user.click(logFillUp)

    expect(await screen.findByRole('heading', { name: 'Log Fill-up' })).toBeInTheDocument()

    const gallons = screen.getByLabelText(/^gallons$/i)
    const totalCost = screen.getByLabelText(/total\s*cost/i)
    const pricePerGallon = screen.getByLabelText(/price.*gal/i)

    await user.type(gallons, '10')
    await user.type(totalCost, '40')

    // Should not calculate while typing.
    expect(pricePerGallon).toHaveValue('')

    // Should calculate on blur (field loses focus).
    await user.click(screen.getByLabelText(/odometer/i))

    expect(Number((pricePerGallon as HTMLInputElement).value)).toBeCloseTo(4, 6)
    expect(screen.getByText(/^calc$/i)).toBeInTheDocument()
  })

  it("Log Fill-up: when all three fuel fields are inconsistent, shows the inline note and recalculates price/gal from gallons+total cost", async () => {
    const vehicleId = '2a8d67b1-1c07-4c9b-a7f1-f9f3d3a2b2df'
    const fetchMock = vi.mocked(fetch)

    fetchMock.mockImplementation(async (input) => {
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

      if (url.endsWith(`/api/v1/garage/vehicles/${vehicleId}`)) {
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

      if (url.endsWith(`/api/v1/garage/vehicles/${vehicleId}/fuel-logs`)) {
        return jsonResponse(
          {
            vehicleId,
            logs: [],
            lastOdometer: 44800,
          },
          { status: 200 },
        )
      }

      throw new Error(`Unexpected fetch: ${url}`)
    })

    await renderAt(`/vehicles/${vehicleId}`)

    const user = userEvent.setup()

    const logFillUp = await screen.findByRole('button', { name: 'Log Fill-up' })
    expect(logFillUp).toBeEnabled()
    await user.click(logFillUp)

    expect(await screen.findByRole('heading', { name: 'Log Fill-up' })).toBeInTheDocument()

    const gallons = screen.getByLabelText(/^gallons$/i)
    const totalCost = screen.getByLabelText(/total\s*cost/i)
    const pricePerGallon = screen.getByLabelText(/price.*gal/i)

    await user.type(gallons, '10')
    await user.type(totalCost, '40')
    await user.type(pricePerGallon, '5')

    // Trigger re-evaluation on blur.
    await user.click(screen.getByLabelText(/odometer/i))

    expect(
      screen.getByText(
        "These numbers don't quite add up — we'll use gallons and total cost to recalculate price/gal.",
      ),
    ).toBeInTheDocument()

    // Priority rule: gallons + totalCost are treated as source of truth; price/gal is recalculated.
    expect(Number((pricePerGallon as HTMLInputElement).value)).toBeCloseTo(4, 6)
  })

  it('Fuel tab: renders logs reverse-chronological by date even if API returns them out of order', async () => {
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

      if (url.endsWith(`/api/v1/garage/vehicles/${vehicleId}/fuel-logs`)) {
        // Intentionally return OLDEST first; UI should sort newest-first.
        return jsonResponse(
          {
            vehicleId,
            lastOdometer: 45230,
            logs: [
              {
                fuelLogId: 'older',
                date: '2026-03-01',
                odometer: 44800,
                gallons: 11.0,
                totalCost: 35.2,
                pricePerGallon: 3.2,
                calculatedField: null,
                isPartialFillUp: false,
                notes: null,
                mpg: null,
                createdAt: '2026-03-01T14:30:00Z',
              },
              {
                fuelLogId: 'newer',
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

      throw new Error(`Unexpected fetch: ${url}`)
    })

    await renderAt(`/vehicles/${vehicleId}`)

    const history = await screen.findByRole('region', { name: /fuel log history/i })
    const cards = Array.from(history.querySelectorAll('article'))
    expect(cards.length).toBeGreaterThanOrEqual(2)

    // Newest card should render first.
    expect(cards[0]?.textContent ?? '').toContain('Mar 15, 2026')
    expect(cards[1]?.textContent ?? '').toContain('Mar 1, 2026')
  })

  it('Log Fill-up: editing a calculated field should not overwrite the user; it should reassign the calculated field using LIFO', async () => {
    const vehicleId = '2a8d67b1-1c07-4c9b-a7f1-f9f3d3a2b2df'
    const fetchMock = vi.mocked(fetch)

    fetchMock.mockImplementation(async (input) => {
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

      if (url.endsWith(`/api/v1/garage/vehicles/${vehicleId}`)) {
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

      if (url.endsWith(`/api/v1/garage/vehicles/${vehicleId}/fuel-logs`)) {
        return jsonResponse(
          {
            vehicleId,
            logs: [],
            lastOdometer: 44800,
          },
          { status: 200 },
        )
      }

      throw new Error(`Unexpected fetch: ${url}`)
    })

    await renderAt(`/vehicles/${vehicleId}`)

    const user = userEvent.setup()

    const logFillUp = await screen.findByRole('button', { name: 'Log Fill-up' })
    await user.click(logFillUp)

    const gallons = await screen.findByLabelText(/^gallons$/i)
    const totalCost = screen.getByLabelText(/total\s*cost/i)
    const pricePerGallon = screen.getByLabelText(/price.*gal/i)

    await user.type(gallons, '10')
    await user.type(totalCost, '40')

    // Blur to calculate price/gal.
    await user.click(screen.getByLabelText(/odometer/i))
    expect(Number((pricePerGallon as HTMLInputElement).value)).toBeCloseTo(4, 6)

    // Now override the previously-calculated field.
    await user.clear(pricePerGallon)
    await user.type(pricePerGallon, '5')

    // Blur should reassign calculated field (LIFO): totalCost + price ⇒ gallons = 8 (calculated)
    await user.click(screen.getByLabelText(/odometer/i))

    expect(screen.queryByRole('note')).not.toBeInTheDocument()
    expect((pricePerGallon as HTMLInputElement).value).toBe('5')
    expect((gallons as HTMLInputElement).value).toBe('8')

    const gallonsRow = (gallons as HTMLInputElement).parentElement
    const priceRow = (pricePerGallon as HTMLInputElement).parentElement
    expect(gallonsRow).not.toBeNull()
    expect(priceRow).not.toBeNull()
    expect(within(gallonsRow!).getByText(/^calc$/i)).toBeInTheDocument()
    expect(within(priceRow!).queryByText(/^calc$/i)).not.toBeInTheDocument()
  })

  it('Log Fill-up: gallons=0 must not produce Infinity/NaN in the inconsistent-all-three path', async () => {
    const vehicleId = '2a8d67b1-1c07-4c9b-a7f1-f9f3d3a2b2df'
    const fetchMock = vi.mocked(fetch)

    fetchMock.mockImplementation(async (input) => {
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

      if (url.endsWith(`/api/v1/garage/vehicles/${vehicleId}`)) {
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

      if (url.endsWith(`/api/v1/garage/vehicles/${vehicleId}/fuel-logs`)) {
        return jsonResponse(
          {
            vehicleId,
            logs: [],
            lastOdometer: 44800,
          },
          { status: 200 },
        )
      }

      throw new Error(`Unexpected fetch: ${url}`)
    })

    await renderAt(`/vehicles/${vehicleId}`)

    const user = userEvent.setup()
    await user.click(await screen.findByRole('button', { name: 'Log Fill-up' }))

    const gallons = await screen.findByLabelText(/^gallons$/i)
    const totalCost = screen.getByLabelText(/total\s*cost/i)
    const pricePerGallon = screen.getByLabelText(/price.*gal/i)

    await user.type(gallons, '0')
    await user.type(totalCost, '40')
    await user.type(pricePerGallon, '5')

    await user.click(screen.getByLabelText(/odometer/i))

    expect((pricePerGallon as HTMLInputElement).value).not.toMatch(/infinity|nan/i)
  })
})
