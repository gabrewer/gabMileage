import React from 'react'
import {
  RouterProvider,
  createMemoryHistory,
  createRouter,
} from '@tanstack/react-router'
import { render, screen } from '@testing-library/react'
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

describe('TASK-006 vehicle detail header/edit/tabs', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn())
  })

  it('Inline edit saves name/make/model/year and updates header (including initial-letter placeholder) without navigating away', async () => {
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

    await renderAt(`/vehicles/${vehicleId}`)

    // Baseline header render
    expect(screen.getByRole('heading', { name: 'Civic' })).toBeInTheDocument()

    const user = userEvent.setup()

    await user.click(screen.getByRole('button', { name: /edit/i }))

    await user.clear(screen.getByLabelText('Vehicle Name'))
    await user.type(screen.getByLabelText('Vehicle Name'), 'Accord')

    await user.clear(screen.getByLabelText('Make'))
    await user.type(screen.getByLabelText('Make'), 'Honda')

    await user.clear(screen.getByLabelText('Model'))
    await user.type(screen.getByLabelText('Model'), 'Accord')

    await user.clear(screen.getByLabelText('Year'))
    await user.type(screen.getByLabelText('Year'), '2020')

    await user.click(screen.getByRole('button', { name: /^save$/i }))

    // Header should update in-place without route navigation.
    expect(await screen.findByRole('heading', { name: 'Accord' })).toBeInTheDocument()

    // Sprint brief: header includes a large colored initial-letter circle placeholder when no photo is set.
    // Expect it to update alongside the header.
    const initial = screen.getByTestId('vehicle-initial-circle')
    expect(initial).toHaveTextContent('A')
  })

  it('Fuel and Maintenance tabs render empty states with disabled CTAs, and tabs are truly non-interactive during edit (a11y)', async () => {
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

      if (url.endsWith(`/api/v1/garage/vehicles/${vehicleId}`) && init?.method === 'PUT') {
        return jsonResponse({ vehicleId }, { status: 200 })
      }

      throw new Error(`Unexpected fetch: ${url}`)
    })

    await renderAt(`/vehicles/${vehicleId}`)

    // Fuel placeholder + disabled CTA
    expect(
      screen.getByText('No fill-ups yet.').closest('section'),
    ).toBeInTheDocument()
    expect(
      screen.getByText('Log your first one to start tracking fuel economy.'),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Log Fill-up' })).toBeDisabled()

    const user = userEvent.setup()

    // Maintenance placeholder + disabled CTA
    await user.click(screen.getByRole('tab', { name: 'Maintenance' }))

    expect(screen.getByText('No maintenance items defined.')).toBeInTheDocument()
    expect(screen.getByText('Add your first one to stay on track.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Add Item' })).toBeDisabled()

    // Enter edit mode; tab area should be non-interactive.
    await user.click(screen.getByRole('button', { name: /edit/i }))

    // For accessibility, tabs should be disabled (not just pointer-events).
    expect(screen.getByRole('tab', { name: 'Fuel' })).toBeDisabled()
    expect(screen.getByRole('tab', { name: 'Maintenance' })).toBeDisabled()
  })

  it('Navigating to an unknown vehicleId shows the 404 message with a back-to-garage link', async () => {
    const vehicleId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
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
            type: 'https://httpstatuses.com/404',
            title: 'Not Found',
            status: 404,
            detail: 'Vehicle not found.',
          },
          { status: 404 },
        )
      }

      throw new Error(`Unexpected fetch: ${url}`)
    })

    await renderAt(`/vehicles/${vehicleId}`)

    // Spec copy (Sprint 02 brief): "doesn't" (straight apostrophe).
    // This should be exact so the UX copy stays consistent.
    expect(
      screen.getByRole('heading', {
        name: "This vehicle doesn't exist or has been removed.",
      }),
    ).toBeInTheDocument()

    expect(screen.getByRole('link', { name: /back to garage/i })).toHaveAttribute(
      'href',
      '/',
    )
  })
})
