import React from 'react'
import {
  RouterProvider,
  createMemoryHistory,
  createRouter,
} from '@tanstack/react-router'
import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { routeTree } from '../routeTree.gen'

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

async function renderRouterAt(path: string) {
  const router = createRouter({
    routeTree,
    history: createMemoryHistory({
      initialEntries: [path],
    }),
  })

  render(<RouterProvider router={router} />)
  await router.load()

  return router
}

describe('TASK-006 Destroyer — adversarial vehicle detail', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn())
  })

  it('does not leak state across vehicleId navigation (header + save target must follow URL)', async () => {
    const v1 = {
      vehicleId: '11111111-1111-1111-1111-111111111111',
      name: 'Alpha',
      make: null,
      model: null,
      year: null,
      photoUrl: null,
      photoThumbnailUrl: null,
      createdAt: '2026-03-23T01:45:00Z',
      updatedAt: '2026-03-23T01:45:00Z',
    }

    const v2 = {
      vehicleId: '22222222-2222-2222-2222-222222222222',
      name: 'Beta',
      make: null,
      model: null,
      year: null,
      photoUrl: null,
      photoThumbnailUrl: null,
      createdAt: '2026-03-23T01:46:00Z',
      updatedAt: '2026-03-23T01:46:00Z',
    }

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

      if (url.endsWith(`/api/v1/garage/vehicles/${v1.vehicleId}`) && init?.method === 'PUT') {
        // If this fires after navigating to v2, the UI is saving to the wrong vehicle.
        return jsonResponse(
          { type: 'https://httpstatuses.com/500', title: 'Wrong target', status: 500 },
          { status: 500 },
        )
      }

      if (url.endsWith(`/api/v1/garage/vehicles/${v1.vehicleId}`) && init?.method === 'GET') {
        return jsonResponse(v1, { status: 200 })
      }

      if (url.endsWith(`/api/v1/garage/vehicles/${v2.vehicleId}`) && init?.method === 'GET') {
        return jsonResponse(v2, { status: 200 })
      }

      if (url.endsWith(`/api/v1/garage/vehicles/${v2.vehicleId}`) && init?.method === 'PUT') {
        return jsonResponse({ vehicleId: v2.vehicleId }, { status: 200 })
      }

      throw new Error(`Unexpected fetch: ${url} (${init?.method ?? 'GET'})`)
    })

    const router = await renderRouterAt(`/vehicles/${v1.vehicleId}`)

    expect(screen.getByRole('heading', { name: 'Alpha' })).toBeInTheDocument()

    // Navigate to another vehicle WITHOUT leaving the route pattern.
    await router.navigate({
      to: '/vehicles/$vehicleId',
      params: { vehicleId: v2.vehicleId },
    })

    expect(await screen.findByRole('heading', { name: 'Beta' })).toBeInTheDocument()

    // Save from the v2 page must PUT to v2, never v1.
    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: /^edit$/i }))

    await user.clear(screen.getByLabelText('Vehicle Name'))
    await user.type(screen.getByLabelText('Vehicle Name'), 'Bravo')

    // Use a sync click to approximate double-tap timing in the browser.
    fireEvent.click(screen.getByRole('button', { name: /^save$/i }))

    expect(await screen.findByRole('heading', { name: 'Bravo' })).toBeInTheDocument()

    const putUrls = fetchMock.mock.calls
      .filter(([, i]) => i?.method === 'PUT')
      .map(([u]) => urlOf(u))

    expect(putUrls).toEqual([
      expect.stringContaining(`/api/v1/garage/vehicles/${v2.vehicleId}`),
    ])
  })

  it('cancel discards draft edits and re-enables tabs', async () => {
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

      if (url.endsWith(`/api/v1/garage/vehicles/${vehicleId}`) && init?.method === 'GET') {
        return jsonResponse(
          {
            vehicleId,
            name: 'Civic',
            make: 'Honda',
            model: 'Civic',
            year: 2018,
            photoUrl: null,
            photoThumbnailUrl: null,
            createdAt: '2026-03-23T01:45:00Z',
            updatedAt: '2026-03-23T01:45:00Z',
          },
          { status: 200 },
        )
      }

      if (url.endsWith(`/api/v1/garage/vehicles/${vehicleId}`) && init?.method === 'PUT') {
        return jsonResponse({ vehicleId }, { status: 200 })
      }

      throw new Error(`Unexpected fetch: ${url}`)
    })

    await renderRouterAt(`/vehicles/${vehicleId}`)

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: /^edit$/i }))

    expect(screen.getByRole('tab', { name: 'Fuel' })).toBeDisabled()

    await user.clear(screen.getByLabelText('Vehicle Name'))
    await user.type(screen.getByLabelText('Vehicle Name'), 'Accord')

    await user.click(screen.getByRole('button', { name: /^cancel$/i }))

    // Draft should be thrown away and the header should remain the original.
    expect(screen.getByRole('heading', { name: 'Civic' })).toBeInTheDocument()

    // Tabs should be interactive again.
    expect(screen.getByRole('tab', { name: 'Fuel' })).toBeEnabled()
    expect(screen.getByRole('tab', { name: 'Maintenance' })).toBeEnabled()
  })
})
