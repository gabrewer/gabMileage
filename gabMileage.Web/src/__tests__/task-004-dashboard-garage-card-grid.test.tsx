import React from 'react'
import {
  RouterProvider,
  createMemoryHistory,
  createRouter,
} from '@tanstack/react-router'
import {
  cleanup,
  render,
  screen,
  within,
} from '@testing-library/react'
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

describe('TASK-004 Dashboard: garage card grid populated state', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn())
  })

  it('shows "Your Garage" heading and a "+ Add Vehicle" button while loading (no empty-state flash)', async () => {
    const fetchMock = vi.mocked(fetch)

    let resolveGarage: ((value: Response) => void) | undefined

    fetchMock.mockImplementation((input) => {
      const url = urlOf(input)

      if (url.endsWith('/api/v1/me')) {
        return Promise.resolve(
          jsonResponse(
            {
              userId: 'user-123',
              displayName: 'Gab',
              email: 'gab@example.com',
              avatarUrl: null,
              createdAt: '2026-03-22T23:26:39Z',
            },
            { status: 200 },
          ),
        )
      }

      if (url.endsWith('/api/v1/garage/summary')) {
        return new Promise<Response>((resolve) => {
          resolveGarage = resolve
        })
      }

      return Promise.reject(new Error(`Unexpected fetch: ${url}`))
    })

    await renderAt('/')

    // New UX contract (TASK-004): heading copy + CTA copy.
    expect(screen.getByRole('heading', { name: /your garage/i })).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: /\+\s*add vehicle/i }),
    ).toBeInTheDocument()

    // Loading contract: show skeleton and do NOT flash empty vs populated.
    expect(screen.getByTestId('dashboard-skeleton')).toBeInTheDocument()
    expect(screen.queryByTestId('empty-garage-state')).not.toBeInTheDocument()
    expect(screen.queryByTestId('garage-vehicle-list')).not.toBeInTheDocument()

    // Resolve to empty to complete the flow (ensures the loader wiring stays correct).
    resolveGarage?.(
      jsonResponse(
        {
          userId: 'user-123',
          vehicles: [],
        },
        { status: 200 },
      ),
    )

    expect(await screen.findByTestId('empty-garage-state')).toBeInTheDocument()
  })

  it('orders vehicle cards newest-first (even if the API returns them out of order)', async () => {
    const fetchMock = vi.mocked(fetch)

    const older = {
      vehicleId: '11111111-1111-1111-1111-111111111111',
      name: 'Old Car',
      make: null,
      model: null,
      year: null,
      photoUrl: null,
      photoThumbnailUrl: null,
      createdAt: '2026-03-23T00:00:00Z',
    }

    const newer = {
      vehicleId: '22222222-2222-2222-2222-222222222222',
      name: 'New Car',
      make: 'Honda',
      model: 'Civic',
      year: 2019,
      photoUrl: null,
      photoThumbnailUrl: null,
      createdAt: '2026-03-23T01:00:00Z',
    }

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

      if (url.endsWith('/api/v1/garage/summary')) {
        // Intentionally out-of-order to ensure the UI sorts by createdAt desc.
        return jsonResponse(
          {
            userId: 'user-123',
            vehicles: [older, newer],
          },
          { status: 200 },
        )
      }

      throw new Error(`Unexpected fetch: ${url}`)
    })

    await renderAt('/')

    const list = screen.getByTestId('garage-vehicle-list')
    const cards = within(list).getAllByRole('button')

    // Newest must appear first.
    expect(within(cards[0]).getByText('New Car')).toBeInTheDocument()
    expect(within(cards[1]).getByText('Old Car')).toBeInTheDocument()
  })

  it('renders an initial-letter colored circle placeholder whose color is derived deterministically from vehicleId', async () => {
    const fetchMock = vi.mocked(fetch)

    const vehicleId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'

    const baseVehicle = {
      vehicleId,
      name: 'Blue Thunder',
      make: null,
      model: null,
      year: null,
      photoUrl: null,
      photoThumbnailUrl: null,
      createdAt: '2026-03-23T01:00:00Z',
    }

    const mockFetchWithName = (name: string) => {
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

        if (url.endsWith('/api/v1/garage/summary')) {
          return jsonResponse(
            {
              userId: 'user-123',
              vehicles: [{ ...baseVehicle, name }],
            },
            { status: 200 },
          )
        }

        throw new Error(`Unexpected fetch: ${url}`)
      })
    }

    mockFetchWithName('Blue Thunder')
    await renderAt('/')

    const card = within(screen.getByTestId('garage-vehicle-list')).getByRole('button')

    // TASK-004: placeholder avatar exists + shows first letter of name.
    const avatar1 = within(card).getByTestId(`vehicle-avatar-${vehicleId}`)
    expect(avatar1).toHaveTextContent(/^B$/)

    // TASK-004: color is deterministic from vehicleId (changing name must not change avatar color).
    const color1 = avatar1.style.backgroundColor || avatar1.style.background
    expect(color1).not.toEqual('')

    cleanup()

    mockFetchWithName('Renamed Vehicle')
    await renderAt('/')

    const card2 = within(screen.getByTestId('garage-vehicle-list')).getByRole('button')
    const avatar2 = within(card2).getByTestId(`vehicle-avatar-${vehicleId}`)
    const color2 = avatar2.style.backgroundColor || avatar2.style.background

    expect(color2).toEqual(color1)

    // Subtitle is optional; if make/model/year are unset, we should not render a prompt-like placeholder.
    expect(within(card2).queryByText(/add make\/model\/year/i)).not.toBeInTheDocument()
  })

  it('clicking the newest vehicle card navigates to the correct vehicle detail page', async () => {
    const fetchMock = vi.mocked(fetch)

    const older = {
      vehicleId: '33333333-3333-3333-3333-333333333333',
      name: 'Older Vehicle',
      make: null,
      model: null,
      year: null,
      photoUrl: null,
      photoThumbnailUrl: null,
      createdAt: '2026-03-23T00:00:00Z',
    }

    const newer = {
      vehicleId: '44444444-4444-4444-4444-444444444444',
      name: 'Newest Vehicle',
      make: null,
      model: null,
      year: null,
      photoUrl: null,
      photoThumbnailUrl: null,
      createdAt: '2026-03-23T02:00:00Z',
    }

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

      if (url.endsWith('/api/v1/garage/summary')) {
        // Out-of-order response: UI should still treat `newer` as first card.
        return jsonResponse(
          {
            userId: 'user-123',
            vehicles: [older, newer],
          },
          { status: 200 },
        )
      }

      if (url.endsWith(`/api/v1/garage/vehicles/${older.vehicleId}`)) {
        return jsonResponse(
          {
            ...older,
            updatedAt: older.createdAt,
          },
          { status: 200 },
        )
      }

      if (url.endsWith(`/api/v1/garage/vehicles/${newer.vehicleId}`)) {
        return jsonResponse(
          {
            ...newer,
            updatedAt: newer.createdAt,
          },
          { status: 200 },
        )
      }

      throw new Error(`Unexpected fetch: ${url}`)
    })

    await renderAt('/')

    const list = screen.getByTestId('garage-vehicle-list')
    const cards = within(list).getAllByRole('button')

    const user = userEvent.setup()
    await user.click(cards[0])

    // If the UI sorts newest-first, clicking the first card should navigate to `newer`.
    expect(await screen.findByRole('heading', { name: /newest vehicle/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /back to garage/i })).toBeInTheDocument()
  })
})
