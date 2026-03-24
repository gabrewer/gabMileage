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

describe('TASK-006 auth shell + login + empty garage UX', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn())
  })

  it('Route / when unauthenticated shows login screen with a single Sign in button', async () => {
    const fetchMock = vi.mocked(fetch)

    fetchMock.mockImplementation(async (input) => {
      const url = urlOf(input)
      if (url.endsWith('/api/v1/me')) {
        return jsonResponse(
          {
            type: 'https://httpstatuses.com/401',
            title: 'Unauthorized',
            status: 401,
            detail: 'Authentication required.',
          },
          { status: 401 },
        )
      }

      throw new Error(`Unexpected fetch: ${url}`)
    })

    await renderAt('/')

    expect(screen.getByTestId('login-screen')).toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: /sign in/i })).toHaveLength(1)
  })

  it('After sign-in, dashboard shows global top bar and the empty garage state when no vehicles exist', async () => {
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
            vehicles: [],
          },
          { status: 200 },
        )
      }

      throw new Error(`Unexpected fetch: ${url}`)
    })

    await renderAt('/')

    expect(screen.getByTestId('topbar')).toBeInTheDocument()
    expect(screen.getByTestId('user-menu-trigger')).toHaveTextContent('Gab')

    expect(screen.getByTestId('empty-garage-state')).toBeInTheDocument()
    expect(
      screen.getByRole('button', {
        name: /\+\s*add vehicle/i,
      }),
    ).toBeInTheDocument()
  })

  it('Authenticated top bar includes user dropdown with Sign out', async () => {
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
            vehicles: [],
          },
          { status: 200 },
        )
      }

      throw new Error(`Unexpected fetch: ${url}`)
    })

    await renderAt('/')

    const user = userEvent.setup()

    await user.click(screen.getByTestId('user-menu-trigger'))

    expect(screen.getByRole('menu')).toBeInTheDocument()
    expect(screen.getByRole('menuitem', { name: /sign out/i })).toBeInTheDocument()
  })

  it('Dashboard uses a skeleton while loading and never flashes empty vs populated state', async () => {
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

    expect(screen.getByTestId('dashboard-skeleton')).toBeInTheDocument()
    expect(screen.queryByTestId('empty-garage-state')).not.toBeInTheDocument()
    expect(screen.queryByTestId('garage-vehicle-list')).not.toBeInTheDocument()

    resolveGarage?.(
      jsonResponse(
        {
          userId: 'user-123',
          vehicles: [],
        },
        { status: 200 },
      ),
    )

    // Once loaded, skeleton must be removed before showing the empty state.
    expect(await screen.findByTestId('empty-garage-state')).toBeInTheDocument()
    expect(screen.queryByTestId('dashboard-skeleton')).not.toBeInTheDocument()
  })

  it('Route /auth/redirect shows the auth redirect loading screen', async () => {
    await renderAt('/auth/redirect?returnTo=/')

    expect(screen.getByTestId('auth-redirect-screen')).toBeInTheDocument()
    expect(screen.getByText(/signing you in\u2026/i)).toBeInTheDocument()
  })
})
