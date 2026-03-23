import React from 'react'
import { RouterProvider, createMemoryHistory, createRouter } from '@tanstack/react-router'
import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

// Force the Dashboard route's loader to hit its "fast" path deterministically:
// - getGarageSummary rejects immediately (microtask)
// - Promise.race resolves with fast.kind === 'done'
// - current implementation throws instead of rendering the retryable in-page error UI
vi.mock('../api/garage', async () => {
  const actual = await vi.importActual<typeof import('../api/garage')>('../api/garage')
  return {
    ...actual,
    getGarageSummary: () => Promise.reject(new Error('boom')),
  }
})

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

async function renderAt(path: string) {
  const { routeTree } = await import('../routeTree.gen')

  const router = createRouter({
    routeTree,
    history: createMemoryHistory({ initialEntries: [path] }),
  })

  render(<RouterProvider router={router} />)

  // If any loader throws, we still want the test to be able to assert what the UI did.
  await router.load().catch(() => undefined)

  return router
}

describe('TASK-004 Destroyer: dashboard garage error handling', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn())
  })

  it('should render an in-page retryable error state even when the garage request fails on the loader fast path', async () => {
    const fetchMock = vi.mocked(fetch)

    fetchMock.mockImplementation(async (input) => {
      const url = urlOf(input)

      // Authenticated layout depends on /api/v1/me.
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

    await renderAt('/')

    // Contract: retry-on-error pattern should be reachable, regardless of how fast the request fails.
    expect(await screen.findByTestId('garage-load-error')).toBeInTheDocument()
  })
})
