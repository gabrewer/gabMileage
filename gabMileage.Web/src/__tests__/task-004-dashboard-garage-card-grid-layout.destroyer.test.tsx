import React from 'react'
import { RouterProvider, createMemoryHistory, createRouter } from '@tanstack/react-router'
import { render, screen } from '@testing-library/react'
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

describe('TASK-004 Destroyer: dashboard responsive grid contract', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn())
  })

  it('does not use auto-fit/auto-fill for the garage grid (needs explicit 1/2/3-column breakpoints per brief)', async () => {
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

      if (url.endsWith('/api/v1/garage/summary')) {
        return jsonResponse(
          {
            userId: 'user-123',
            vehicles: [
              {
                vehicleId: '11111111-1111-1111-1111-111111111111',
                name: 'Car A',
                make: null,
                model: null,
                year: null,
                photoUrl: null,
                photoThumbnailUrl: null,
                createdAt: '2026-03-23T01:00:00Z',
              },
              {
                vehicleId: '22222222-2222-2222-2222-222222222222',
                name: 'Car B',
                make: null,
                model: null,
                year: null,
                photoUrl: null,
                photoThumbnailUrl: null,
                createdAt: '2026-03-23T02:00:00Z',
              },
            ],
          },
          { status: 200 },
        )
      }

      throw new Error(`Unexpected fetch: ${url}`)
    })

    await renderAt('/')

    const list = screen.getByTestId('garage-vehicle-list')
    // Proxy assertion: auto-fit/auto-fill cannot guarantee 1-col mobile / 2-col tablet / 3-col desktop.
    expect(list.style.gridTemplateColumns).not.toMatch(/auto-fit|auto-fill/i)
  })
})
