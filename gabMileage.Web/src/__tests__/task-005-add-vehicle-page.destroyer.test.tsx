import React from 'react'
import { RouterProvider, createMemoryHistory, createRouter } from '@tanstack/react-router'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { routeTree } from '../routeTree.gen'
import { validateVehicleDraft } from '../lib/vehicleValidation'

async function renderAt(path: string) {
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

describe('TASK-005 Destroyer — Add Vehicle', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn())
  })

  it('validateVehicleDraft enforces year bounds [1900..currentYear+1] and accepts empty year', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-03-23T00:00:00Z'))

    try {
      const base = { name: 'Car', make: '', model: '', year: '' }

      expect(validateVehicleDraft(base).value?.year).toBeNull()

      expect(validateVehicleDraft({ ...base, year: '1899' }).value).toBeNull()
      expect(validateVehicleDraft({ ...base, year: '1900' }).value?.year).toBe(1900)

      // currentYear=2026 => max allowed = 2027
      expect(validateVehicleDraft({ ...base, year: '2027' }).value?.year).toBe(2027)
      expect(validateVehicleDraft({ ...base, year: '2028' }).value).toBeNull()
    } finally {
      vi.useRealTimers()
    }
  })

  it('Add Vehicle request must send trimmed values and null optional blanks', async () => {
    const fetchMock = vi.mocked(fetch)

    let addVehicleBody: any | undefined

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
        addVehicleBody = JSON.parse(String(init.body ?? 'null'))
        return jsonResponse({ vehicleId: 'veh-123' }, { status: 200 })
      }

      if (url.endsWith('/api/v1/garage/vehicles/veh-123')) {
        return jsonResponse(
          {
            vehicleId: 'veh-123',
            name: 'My Subaru',
            make: 'Honda',
            model: 'Civic',
            year: 2020,
            photoUrl: null,
            photoThumbnailUrl: null,
            createdAt: '2026-03-22T23:26:39Z',
            updatedAt: '2026-03-22T23:26:39Z',
          },
          { status: 200 },
        )
      }

      throw new Error(`Unexpected fetch: ${url}`)
    })

    const router = await renderAt('/vehicles/new')

    const user = userEvent.setup()

    await user.type(screen.getByLabelText(/vehicle name/i), '  My Subaru  ')
    await user.type(screen.getByLabelText(/^make$/i), '  Honda  ')
    await user.type(screen.getByLabelText(/^model$/i), '  Civic  ')
    await user.type(screen.getByLabelText(/^year$/i), '2020')

    await user.click(screen.getByRole('button', { name: /add to garage/i }))

    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/vehicles/veh-123')
    })

    expect(addVehicleBody).toEqual({
      commandId: expect.any(String),
      name: 'My Subaru',
      make: 'Honda',
      model: 'Civic',
      year: 2020,
    })
  })

  it('Non-validation failure shows the inline banner error', async () => {
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

      if (url.endsWith('/api/v1/garage/vehicles') && init?.method === 'POST') {
        return jsonResponse({ title: 'nope' }, { status: 500 })
      }

      throw new Error(`Unexpected fetch: ${url}`)
    })

    await renderAt('/vehicles/new')

    const user = userEvent.setup()

    await user.type(screen.getByLabelText(/vehicle name/i), 'My Subaru')
    await user.click(screen.getByRole('button', { name: /add to garage/i }))

    expect(await screen.findByTestId('vehicle-save-error')).toHaveTextContent(
      "Couldn't save — please try again.",
    )
  })
})
