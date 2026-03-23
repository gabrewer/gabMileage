import React from 'react'
import {
  RouterProvider,
  createMemoryHistory,
  createRouter,
} from '@tanstack/react-router'
import { render, screen, waitFor } from '@testing-library/react'
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

describe('TASK-007 UI happy-path: add vehicle -> detail navigation', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn())
  })

  it('adds a vehicle and renders the detail page with a stable test id', async () => {
    const fetchMock = vi.mocked(fetch)

    const vehicleId = '2a8d67b1-1c07-4c9b-a7f1-f9f3d3a2b2df'

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

      if (url.endsWith('/api/v1/garage/vehicles') && init?.method === 'POST') {
        return jsonResponse({ vehicleId }, { status: 201 })
      }

      if (url.endsWith(`/api/v1/garage/vehicles/${vehicleId}`) && (!init || init.method === 'GET')) {
        return jsonResponse(
          {
            vehicleId,
            name: 'My Subaru',
            make: 'Subaru',
            model: 'Outback',
            year: 2020,
            photoUrl: null,
            photoThumbnailUrl: null,
            createdAt: '2026-03-23T01:45:00Z',
            updatedAt: '2026-03-23T01:45:00Z',
          },
          { status: 200 },
        )
      }

      throw new Error(`Unexpected fetch: ${url}`)
    })

    const router = await renderAt('/vehicles/new')

    const user = userEvent.setup()

    await user.type(screen.getByLabelText(/vehicle name/i), 'My Subaru')
    await user.type(screen.getByLabelText(/make/i), 'Subaru')
    await user.type(screen.getByLabelText(/model/i), 'Outback')
    await user.type(screen.getByLabelText(/year/i), '2020')

    await user.click(screen.getByRole('button', { name: /add to garage/i }))

    await waitFor(() => {
      expect(router.state.location.pathname).toBe(`/vehicles/${vehicleId}`)
    })

    // Happy-path render assertion for the detail screen.
    expect(await screen.findByRole('heading', { name: /my subaru/i })).toBeInTheDocument()

    // TASK-007: require a stable selector for the happy-path detail render.
    // (Today we only have test ids for error states; this test drives adding one.)
    expect(screen.getByTestId('vehicle-detail-page')).toBeInTheDocument()
  })
})
