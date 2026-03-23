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

function findPost(fetchMock: ReturnType<typeof vi.fn>, endsWith: string) {
  const call = fetchMock.mock.calls.find(([input, init]) => {
    const url = urlOf(input)
    return url.endsWith(endsWith) && (init as RequestInit | undefined)?.method === 'POST'
  })

  return call as
    | undefined
    | [RequestInfo | URL, RequestInit | undefined]
}

describe('TASK-007 Log Fill-up submit + refresh (unit-test approximation of E2E)', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn())
  })

  it('Save: two-of-three auto-calc posts calculatedField=pricePerGallon with numeric payload', async () => {
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
        return jsonResponse(
          {
            vehicleId,
            logs: [],
            lastOdometer: 44800,
          },
          { status: 200 },
        )
      }

      if (url.endsWith(`/api/v1/garage/vehicles/${vehicleId}/fuel-logs`) && init?.method === 'POST') {
        return jsonResponse(
          {
            fuelLogId: 'new-log-id',
            odometerWarning: null,
          },
          { status: 201 },
        )
      }

      throw new Error(`Unexpected fetch: ${url} (${init?.method ?? 'GET'})`)
    })

    await renderAt(`/vehicles/${vehicleId}`)

    const user = userEvent.setup()
    await user.click(await screen.findByRole('button', { name: 'Log Fill-up' }))

    expect(await screen.findByRole('heading', { name: 'Log Fill-up' })).toBeInTheDocument()

    await user.clear(screen.getByLabelText(/^date$/i))
    await user.type(screen.getByLabelText(/^date$/i), '2026-03-23')

    await user.type(screen.getByLabelText(/odometer/i), '45230')

    const gallons = screen.getByLabelText(/^gallons$/i)
    const totalCost = screen.getByLabelText(/total\s*cost/i)
    const pricePerGallon = screen.getByLabelText(/price.*gal/i)

    await user.type(gallons, '10')
    await user.type(totalCost, '40')

    // Calculate on blur.
    await user.click(screen.getByLabelText(/odometer/i))

    expect(Number((pricePerGallon as HTMLInputElement).value)).toBeCloseTo(4, 6)
    expect(within((pricePerGallon as HTMLInputElement).parentElement!).queryByText(/^calc$/i)).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Save' }))

    const post = findPost(fetchMock as any, `/api/v1/garage/vehicles/${vehicleId}/fuel-logs`)
    expect(post, 'Expected Log Fill-up to POST to recordFuelLog endpoint').toBeDefined()

    const [, init] = post!
    const body = JSON.parse(String(init?.body ?? 'null')) as any

    expect(body).toMatchObject({
      date: '2026-03-23',
      odometer: 45230,
      gallons: 10,
      totalCost: 40,
      pricePerGallon: 4,
      calculatedField: 'pricePerGallon',
      isPartialFillUp: false,
      notes: null,
    })
    expect(body.commandId).toMatch(
      /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i,
    )
  })

  it('Save: LIFO reassignment posts calculatedField=gallons after user overrides a previously-calculated value', async () => {
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
        return jsonResponse({ vehicleId, logs: [], lastOdometer: 44800 }, { status: 200 })
      }

      if (url.endsWith(`/api/v1/garage/vehicles/${vehicleId}/fuel-logs`) && init?.method === 'POST') {
        return jsonResponse({ fuelLogId: 'new-log-id', odometerWarning: null }, { status: 201 })
      }

      throw new Error(`Unexpected fetch: ${url} (${init?.method ?? 'GET'})`)
    })

    await renderAt(`/vehicles/${vehicleId}`)

    const user = userEvent.setup()
    await user.click(await screen.findByRole('button', { name: 'Log Fill-up' }))

    await user.clear(screen.getByLabelText(/^date$/i))
    await user.type(screen.getByLabelText(/^date$/i), '2026-03-23')

    await user.type(screen.getByLabelText(/odometer/i), '45230')

    const gallons = await screen.findByLabelText(/^gallons$/i)
    const totalCost = screen.getByLabelText(/total\s*cost/i)
    const pricePerGallon = screen.getByLabelText(/price.*gal/i)

    await user.type(gallons, '10')
    await user.type(totalCost, '40')

    // Blur to calculate price/gal.
    await user.click(screen.getByLabelText(/odometer/i))
    expect(Number((pricePerGallon as HTMLInputElement).value)).toBeCloseTo(4, 6)

    // Override the previously-calculated field.
    await user.clear(pricePerGallon)
    await user.type(pricePerGallon, '5')

    // Blur should reassign calculated field using LIFO: totalCost + price => gallons = 8.
    await user.click(screen.getByLabelText(/odometer/i))

    expect((pricePerGallon as HTMLInputElement).value).toBe('5')
    expect((gallons as HTMLInputElement).value).toBe('8')

    await user.click(screen.getByRole('button', { name: 'Save' }))

    const post = findPost(fetchMock as any, `/api/v1/garage/vehicles/${vehicleId}/fuel-logs`)
    expect(post, 'Expected Log Fill-up to POST after Save').toBeDefined()

    const [, init] = post!
    const body = JSON.parse(String(init?.body ?? 'null')) as any

    expect(body).toMatchObject({
      date: '2026-03-23',
      odometer: 45230,
      gallons: 8,
      totalCost: 40,
      pricePerGallon: 5,
      calculatedField: 'gallons',
    })
  })

  it("Save: inconsistent triple input posts server-priority values (gallons+totalCost => recalculated price/gal)", async () => {
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
        return jsonResponse({ vehicleId, logs: [], lastOdometer: 44800 }, { status: 200 })
      }

      if (url.endsWith(`/api/v1/garage/vehicles/${vehicleId}/fuel-logs`) && init?.method === 'POST') {
        return jsonResponse({ fuelLogId: 'new-log-id', odometerWarning: null }, { status: 201 })
      }

      throw new Error(`Unexpected fetch: ${url} (${init?.method ?? 'GET'})`)
    })

    await renderAt(`/vehicles/${vehicleId}`)

    const user = userEvent.setup()
    await user.click(await screen.findByRole('button', { name: 'Log Fill-up' }))

    await user.clear(screen.getByLabelText(/^date$/i))
    await user.type(screen.getByLabelText(/^date$/i), '2026-03-23')

    await user.type(screen.getByLabelText(/odometer/i), '45230')

    const gallons = await screen.findByLabelText(/^gallons$/i)
    const totalCost = screen.getByLabelText(/total\s*cost/i)
    const pricePerGallon = screen.getByLabelText(/price.*gal/i)

    await user.type(gallons, '10')
    await user.type(totalCost, '40')
    await user.type(pricePerGallon, '5')

    // Trigger inconsistency resolution on blur.
    await user.click(screen.getByLabelText(/odometer/i))

    expect(
      screen.getByText(
        "These numbers don't quite add up — we'll use gallons and total cost to recalculate price/gal.",
      ),
    ).toBeInTheDocument()

    expect(Number((pricePerGallon as HTMLInputElement).value)).toBeCloseTo(4, 6)

    await user.click(screen.getByRole('button', { name: 'Save' }))

    const post = findPost(fetchMock as any, `/api/v1/garage/vehicles/${vehicleId}/fuel-logs`)
    expect(post, 'Expected Log Fill-up to POST after Save').toBeDefined()

    const [, init] = post!
    const body = JSON.parse(String(init?.body ?? 'null')) as any

    expect(body).toMatchObject({
      gallons: 10,
      totalCost: 40,
      pricePerGallon: 4,
      calculatedField: 'pricePerGallon',
    })
  })
})
