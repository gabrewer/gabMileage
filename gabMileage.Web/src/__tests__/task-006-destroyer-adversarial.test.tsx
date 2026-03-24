import React from 'react'
import {
  RouterProvider,
  createMemoryHistory,
  createRouter,
} from '@tanstack/react-router'
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

describe('TASK-006 Destroyer adversarial cases', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn())
  })

  it('Sanitizes returnTo on /login to avoid open-redirects (absolute URL should be rejected)', async () => {
    await renderAt('/login?returnTo=https://evil.example/phish')

    const returnToInput = document.querySelector(
      'input[name="returnTo"]',
    ) as HTMLInputElement | null

    expect(returnToInput).not.toBeNull()

    // Security expectation: only allow app-internal relative paths.
    // If this fails, an attacker can craft a /login link that bounces users to a phishing URL.
    expect(returnToInput?.value).toBe('/')
  })

  it('If /api/v1/garage/summary returns 401 after /me succeeds, app should redirect to /login (not crash)', async () => {
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

    await expect(renderAt('/')).resolves.toBeUndefined()

    expect(screen.getByTestId('login-screen')).toBeInTheDocument()
  })
})
