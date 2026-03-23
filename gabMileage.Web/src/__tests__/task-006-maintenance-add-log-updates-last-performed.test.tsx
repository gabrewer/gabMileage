import React from 'react'
import {
  RouterProvider,
  createMemoryHistory,
  createRouter,
} from '@tanstack/react-router'
import { render, screen, waitFor, within } from '@testing-library/react'
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

describe('TASK-006 Maintenance key flow (add item → log service → see updated last performed)', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn())
  })

  it('After saving Add Item and Log Service, the vehicle page should keep Maintenance selected and show updated last performed line', async () => {
    const vehicleId = '2a8d67b1-1c07-4c9b-a7f1-f9f3d3a2b2df'
    const itemId = 'item-oil-1'

    const fetchMock = vi.mocked(fetch)

    type MaintState = 'empty' | 'afterAdd' | 'afterLog'
    let state: MaintState = 'empty'

    const maintenanceEmpty = {
      vehicleId,
      lastOdometer: 49600,
      items: [],
    }

    const maintenanceAfterAdd = {
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
          status: 'dueSoon',
          statusDetail: 'No service history — log your first service to start tracking',
          lastPerformedDate: null,
          lastPerformedOdometer: null,
          records: [],
          createdAt: '2026-03-23T02:00:00Z',
        },
      ],
    }

    const maintenanceAfterLog = {
      ...maintenanceAfterAdd,
      items: [
        {
          ...maintenanceAfterAdd.items[0],
          status: 'ok',
          statusDetail: 'Next due in ~5,000 mi',
          lastPerformedDate: '2026-03-23',
          lastPerformedOdometer: 49600,
          records: [
            {
              recordId: 'rec-1',
              itemId,
              date: '2026-03-23',
              odometer: 49600,
              cost: 80,
              notes: null,
              createdAt: '2026-03-23T10:00:00Z',
            },
          ],
        },
      ],
    }

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
        if (state === 'empty') return jsonResponse(maintenanceEmpty, { status: 200 })
        if (state === 'afterAdd') return jsonResponse(maintenanceAfterAdd, { status: 200 })
        return jsonResponse(maintenanceAfterLog, { status: 200 })
      }

      if (url.endsWith(`/api/v1/garage/vehicles/${vehicleId}/maintenance/items`) && init?.method === 'POST') {
        state = 'afterAdd'
        return jsonResponse({ itemId }, { status: 201 })
      }

      if (url.endsWith(`/api/v1/garage/vehicles/${vehicleId}/maintenance/items/${itemId}/records`) && init?.method === 'POST') {
        state = 'afterLog'
        return jsonResponse({ recordId: 'rec-1' }, { status: 201 })
      }

      throw new Error(`Unexpected fetch: ${url} (${init?.method ?? 'GET'})`)
    })

    await renderAt(`/vehicles/${vehicleId}`)

    const user = userEvent.setup()

    // Start on maintenance so the user can add an item.
    await user.click(screen.getByRole('tab', { name: 'Maintenance' }))
    await user.click(await screen.findByRole('button', { name: 'Add Item' }))

    // Add Item form
    expect(await screen.findByRole('heading', { name: /add maintenance item/i })).toBeInTheDocument()
    await user.type(screen.getByLabelText(/^name$/i), 'Oil Change')
    await user.type(screen.getByLabelText(/mileage interval/i), '5000')
    await user.click(screen.getByRole('button', { name: /^save$/i }))

    // Acceptance: returning to the vehicle page should keep Maintenance selected so the user immediately sees the new item.
    await waitFor(() => {
      expect(screen.getByRole('tab', { name: 'Maintenance' })).toHaveAttribute('aria-selected', 'true')
    }, { timeout: 250 })

    // New item should be visible without clicking the tab again.
    const oil = await screen.findByText('Oil Change')
    const oilCard = oil.closest('article')
    expect(oilCard).not.toBeNull()
    expect(within(oilCard!).getByText('Last performed: Never performed')).toBeInTheDocument()

    // Log service for the new item
    await user.click(within(oilCard!).getByRole('button', { name: /log service/i }))
    expect(await screen.findByRole('heading', { name: /log service/i })).toBeInTheDocument()

    await user.clear(screen.getByLabelText(/^date$/i))
    await user.type(screen.getByLabelText(/^date$/i), '2026-03-23')

    await user.type(screen.getByLabelText(/odometer/i), '49600')
    await user.type(screen.getByLabelText(/cost/i), '80')
    await user.click(screen.getByRole('button', { name: /^save$/i }))

    // Acceptance: returning should again keep Maintenance selected and show updated last performed line.
    await waitFor(() => {
      expect(screen.getByRole('tab', { name: 'Maintenance' })).toHaveAttribute('aria-selected', 'true')
    }, { timeout: 250 })

    const oilAfter = await screen.findByText('Oil Change')
    const oilAfterCard = oilAfter.closest('article')
    expect(oilAfterCard).not.toBeNull()

    expect(within(oilAfterCard!).getByText(/Last performed: Mar 23, 2026 at 49,600 mi/)).toBeInTheDocument()
  })
})
