// @vitest-environment node
import { beforeEach, describe, expect, it, vi } from 'vitest'

// SSR-only module: fetchJson dynamically imports this.
vi.mock('@tanstack/start-server-core', () => ({
  getRequestHeader: (name: string) => (name.toLowerCase() === 'cookie' ? 'gab_session=SUPERSECRET' : undefined),
}))

import { fetchJson } from '../api/http'

describe('Destroyer: SSR cookie forwarding', () => {
  beforeEach(() => {
    // Ensure we are in SSR mode.
    // (vitest-environment node already avoids window)
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        return {
          ok: true,
          status: 200,
          headers: new Headers({ 'content-type': 'application/json' }),
          json: async () => ({ ok: true }),
          text: async () => JSON.stringify({ ok: true }),
        } as any
      }),
    )
  })

  it('for relative URLs, forwards the incoming Cookie header (expected)', async () => {
    await fetchJson('/api/v1/me')

    const fetchMock = vi.mocked(fetch)
    const [, init] = fetchMock.mock.calls[0]!
    const headers = (init?.headers ?? {}) as Record<string, string>

    expect(headers.cookie ?? headers.Cookie).toContain('gab_session=SUPERSECRET')
  })

  it('should NOT forward cookies to absolute/external URLs (prevents leaking session to third parties)', async () => {
    await fetchJson('https://evil.example.test/collect')

    const fetchMock = vi.mocked(fetch)
    const [, init] = fetchMock.mock.calls[0]!
    const headers = (init?.headers ?? {}) as Record<string, string>

    // If this fails, SSR will leak the user session cookie to any absolute URL a developer fetches.
    expect(headers.cookie ?? headers.Cookie).toBeUndefined()
  })
})
