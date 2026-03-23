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

const uuidV4Regex =
  /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i

describe('TASK-005 Add Vehicle page', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn())
  })

  it('Submitting with blank/whitespace-only name shows "Your vehicle needs a name"', async () => {
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

      throw new Error(`Unexpected fetch: ${url}`)
    })

    await renderAt('/vehicles/new')

    const user = userEvent.setup()

    // Whitespace-only name should be rejected.
    await user.type(screen.getByLabelText(/vehicle name/i), '   ')
    await user.click(screen.getByRole('button', { name: /add to garage/i }))

    expect(screen.getByText('Your vehicle needs a name')).toBeInTheDocument()
  })

  it('Successful save disables the form, calls the endpoint with a UUID commandId, navigates to detail, and shows the success toast', async () => {
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
            make: null,
            model: null,
            year: null,
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

    await user.type(screen.getByLabelText(/vehicle name/i), 'My Subaru')

    const submit = screen.getByRole('button', { name: /add to garage/i })
    await user.click(submit)

    // Busy state: UX spec says the form should be disabled and show a spinner.
    // We assert a concrete, accessible "busy" signal.
    expect(screen.getByTestId('add-vehicle-form')).toHaveAttribute('aria-busy', 'true')

    expect(addVehicleBody).toBeDefined()
    expect(addVehicleBody.commandId).toMatch(uuidV4Regex)

    // Navigate to the new vehicle detail page.
    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/vehicles/veh-123')
    })

    // Detail page should render the vehicle name.
    expect(await screen.findByRole('heading', { name: 'My Subaru' })).toBeInTheDocument()

    // Success toast should be visible and stable copy.
    expect(await screen.findByText('My Subaru added to your garage!')).toBeInTheDocument()
  })

  it('Duplicate-clicking submit does not create duplicate vehicles (button disabled + idempotency key)', async () => {
    const fetchMock = vi.mocked(fetch)

    // Simulate an environment where crypto.randomUUID is missing; commandId must still be a UUID.
    const originalCrypto = globalThis.crypto
    ;(globalThis as any).crypto = {}

    let postCount = 0
    let lastBody: any | undefined
    let resolveAdd: ((value: Response) => void) | undefined

    fetchMock.mockImplementation((input, init) => {
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

      if (url.endsWith('/api/v1/garage/vehicles') && init?.method === 'POST') {
        postCount += 1
        lastBody = JSON.parse(String(init.body ?? 'null'))
        return new Promise<Response>((resolve) => {
          resolveAdd = resolve
        })
      }

      return Promise.reject(new Error(`Unexpected fetch: ${url}`))
    })

    try {
      await renderAt('/vehicles/new')

      const user = userEvent.setup()

      await user.type(screen.getByLabelText(/vehicle name/i), 'My Subaru')
      const submit = screen.getByRole('button', { name: /add to garage/i })

      // Fire two clicks without awaiting between them to simulate a fast double-tap.
      const c1 = user.click(submit)
      const c2 = user.click(submit)
      await Promise.allSettled([c1, c2])

      expect(postCount).toBe(1)
      expect(lastBody?.commandId).toMatch(uuidV4Regex)

      resolveAdd?.(jsonResponse({ vehicleId: 'veh-123' }, { status: 200 }))
    } finally {
      ;(globalThis as any).crypto = originalCrypto
    }
  })
})
