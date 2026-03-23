import React from 'react'
import { RouterProvider, createMemoryHistory, createRouter } from '@tanstack/react-router'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { routeTree } from '../routeTree.gen'

async function renderAt(path: string) {
  const router = createRouter({
    routeTree,
    history: createMemoryHistory({ initialEntries: [path] }),
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

function parseBody(init?: RequestInit) {
  if (!init?.body) return null
  if (typeof init.body === 'string') return JSON.parse(init.body)
  throw new Error('Expected JSON string body')
}

describe('TASK-005 Destroyer — Maintenance UI domain edge cases', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn())
  })

  it('Log Service: leaving odometer blank must not submit odometer=0', async () => {
    const vehicleId = '2a8d67b1-1c07-4c9b-a7f1-f9f3d3a2b2df'
    const itemId = 'f8a7f8b2-3a3f-4b50-93b0-5c8accc7bbf3'

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
        return jsonResponse({ vehicleId, logs: [], lastOdometer: 49600 }, { status: 200 })
      }

      if (url.endsWith(`/api/v1/garage/vehicles/${vehicleId}/maintenance`) && (!init || init.method === 'GET')) {
        return jsonResponse(
          {
            vehicleId,
            lastOdometer: 49600,
            items: [
              {
                itemId,
                name: 'Oil Change',
                mileageInterval: 5000,
                timeIntervalMonths: 6,
                mileageWarning: 500,
                timeWarningDays: 14,
                status: 'ok',
                statusDetail: 'Next due in ~5,000 mi',
                lastPerformedDate: null,
                lastPerformedOdometer: null,
                records: [],
                createdAt: '2026-03-23T02:00:00Z',
              },
            ],
          },
          { status: 200 },
        )
      }

      if (
        url.endsWith(`/api/v1/garage/vehicles/${vehicleId}/maintenance/items/${itemId}/records`) &&
        init?.method === 'POST'
      ) {
        return jsonResponse({ recordId: 'rec-1' }, { status: 201 })
      }

      throw new Error(`Unexpected fetch: ${url} (${init?.method ?? 'GET'})`)
    })

    await renderAt(`/vehicles/${vehicleId}`)

    const user = userEvent.setup()
    await user.click(screen.getByRole('tab', { name: 'Maintenance' }))

    const card = await screen.findByText('Oil Change')
    const article = card.closest('article')
    expect(article).not.toBeNull()

    await user.click(screen.getByRole('button', { name: /log service/i }))

    expect(await screen.findByRole('heading', { name: /log service/i })).toBeInTheDocument()

    // Intentionally leave odometer blank.
    await user.clear(screen.getByLabelText(/odometer/i))
    await user.click(screen.getByRole('button', { name: /^save$/i }))

    // If the UI blocks submission, it should show an error. If it submits, it must not send odometer=0.
    let postCall: [RequestInfo | URL, RequestInit | undefined] | undefined
    try {
      await waitFor(() => {
        postCall = (fetchMock as any).mock.calls.find(([i, req]: any[]) => {
          const u = urlOf(i)
          return u.endsWith(`/api/v1/garage/vehicles/${vehicleId}/maintenance/items/${itemId}/records`) &&
            req?.method === 'POST'
        })
        expect(postCall).toBeTruthy()
      }, { timeout: 150 })
    } catch {
      // ignore
    }

    if (!postCall) {
      // Expect a client-side validation surface.
      expect(await screen.findByRole('alert')).toHaveTextContent(/odometer/i)
      return
    }

    const body = parseBody(postCall[1]) as any
    expect(body?.odometer, 'Blank odometer must not be coerced to 0').toBeGreaterThan(0)
  })

  it('Add Maintenance Item: interval=0 must not be submitted as a valid interval', async () => {
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
        return jsonResponse({ vehicleId, logs: [], lastOdometer: 49600 }, { status: 200 })
      }

      if (url.endsWith(`/api/v1/garage/vehicles/${vehicleId}/maintenance`) && (!init || init.method === 'GET')) {
        return jsonResponse({ vehicleId, items: [], lastOdometer: 49600 }, { status: 200 })
      }

      if (url.endsWith(`/api/v1/garage/vehicles/${vehicleId}/maintenance/items`) && init?.method === 'POST') {
        return jsonResponse({ itemId: 'item-1' }, { status: 201 })
      }

      throw new Error(`Unexpected fetch: ${url} (${init?.method ?? 'GET'})`)
    })

    await renderAt(`/vehicles/${vehicleId}`)

    const user = userEvent.setup()
    await user.click(screen.getByRole('tab', { name: 'Maintenance' }))
    await user.click(await screen.findByRole('button', { name: 'Add Item' }))

    expect(await screen.findByRole('heading', { name: /add maintenance item/i })).toBeInTheDocument()

    await user.type(screen.getByLabelText(/^name$/i), 'Oil Change')
    await user.type(screen.getByLabelText(/mileage interval/i), '0')

    await user.click(screen.getByRole('button', { name: /^save$/i }))

    const post = (fetchMock as any).mock.calls.find(([i, req]: any[]) => {
      const u = urlOf(i)
      return u.endsWith(`/api/v1/garage/vehicles/${vehicleId}/maintenance/items`) && req?.method === 'POST'
    })

    if (!post) {
      expect(await screen.findByRole('alert')).toHaveTextContent(/interval/i)
      return
    }

    const body = parseBody(post[1]) as any
    expect(body?.mileageInterval, 'Interval=0 must not be treated as valid').toBeGreaterThan(0)
  })

  it('Edit Maintenance Item: warning threshold must be < interval (client-side or server-side enforcement)', async () => {
    const vehicleId = '2a8d67b1-1c07-4c9b-a7f1-f9f3d3a2b2df'
    const itemId = 'i-1'
    const fetchMock = vi.mocked(fetch)

    fetchMock.mockImplementation(async (input, init) => {
      const url = urlOf(input)

      if (url.endsWith('/api/v1/me')) {
        return jsonResponse({ userId: 'user-123', displayName: 'Gab', email: 'gab@example.com', avatarUrl: null, createdAt: '2026-03-22T23:26:39Z' }, { status: 200 })
      }

      if (url.endsWith(`/api/v1/garage/vehicles/${vehicleId}`) && (!init || init.method === 'GET')) {
        return jsonResponse({ vehicleId, name: 'Blue Thunder', make: null, model: null, year: null, photoUrl: null, photoThumbnailUrl: null, createdAt: '2026-03-23T01:45:00Z', updatedAt: '2026-03-23T01:45:00Z' }, { status: 200 })
      }

      if (url.endsWith(`/api/v1/garage/vehicles/${vehicleId}/maintenance`) && (!init || init.method === 'GET')) {
        return jsonResponse({
          vehicleId,
          lastOdometer: 49600,
          items: [
            {
              itemId,
              name: 'Oil Change',
              mileageInterval: 5000,
              timeIntervalMonths: null,
              mileageWarning: 500,
              timeWarningDays: 14,
              status: 'ok',
              statusDetail: 'Next due in ~5,000 mi',
              lastPerformedDate: null,
              lastPerformedOdometer: null,
              records: [],
              createdAt: '2026-03-23T02:00:00Z',
            },
          ],
        }, { status: 200 })
      }

      if (
        url.endsWith(`/api/v1/garage/vehicles/${vehicleId}/maintenance/items/${itemId}`) &&
        init?.method === 'PUT'
      ) {
        return jsonResponse({ itemId }, { status: 200 })
      }

      // Vehicle page loader fetches fuel logs; keep it stubbed.
      if (url.endsWith(`/api/v1/garage/vehicles/${vehicleId}/fuel-logs`) && (!init || init.method === 'GET')) {
        return jsonResponse({ vehicleId, logs: [], lastOdometer: 49600 }, { status: 200 })
      }

      throw new Error(`Unexpected fetch: ${url} (${init?.method ?? 'GET'})`)
    })

    await renderAt(`/vehicles/${vehicleId}/maintenance/${itemId}/edit`)

    const user = userEvent.setup()

    expect(await screen.findByRole('heading', { name: /edit maintenance item/i })).toBeInTheDocument()

    // Set an obviously-invalid warning (>= interval).
    await user.clear(screen.getByLabelText(/warn me \(miles before due\)/i))
    await user.type(screen.getByLabelText(/warn me \(miles before due\)/i), '7000')

    await user.click(screen.getByRole('button', { name: /^save$/i }))

    const put = (fetchMock as any).mock.calls.find(([i, req]: any[]) => {
      const u = urlOf(i)
      return u.endsWith(`/api/v1/garage/vehicles/${vehicleId}/maintenance/items/${itemId}`) && req?.method === 'PUT'
    })

    if (!put) {
      expect(await screen.findByRole('alert')).toHaveTextContent(/warning|interval/i)
      return
    }

    const body = parseBody(put[1]) as any
    expect(body?.mileageWarning, 'Warning threshold must be less than interval').toBeLessThan(body?.mileageInterval)
  })
})
