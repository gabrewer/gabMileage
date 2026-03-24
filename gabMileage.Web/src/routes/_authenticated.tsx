import React from 'react'
import { Outlet, createFileRoute, redirect } from '@tanstack/react-router'
import { getMe, isUnauthorized } from '../api/me'
import { TopBar } from '../components/TopBar'
import { returnToFromHref } from '../lib/returnTo'

/**
 * Breadcrumbs (frontend-builder)
 * What: Pathless authenticated layout route that fetches /api/v1/me and gates access.
 * Why: Keeps auth logic out of individual pages; ensures the nav shell appears on all authenticated pages.
 * Alternatives considered: auth checks inside components (rejected; risks flashing protected UI), only guarding / (rejected; won’t scale).
 * Confidence: high
 */

export const Route = createFileRoute('/_authenticated')({
  loader: async ({ location }) => {
    try {
      const me = await getMe()
      return { me }
    } catch (err) {
      if (isUnauthorized(err)) {
        throw redirect({
          to: '/login',
          search: { returnTo: returnToFromHref(location.href) },
        })
      }
      throw err
    }
  },
  component: AuthenticatedLayout,
})

function AuthenticatedLayout() {
  const { me } = Route.useLoaderData()

  return (
    <div style={{ minHeight: '100vh', background: 'rgba(17,24,39,0.02)' }}>
      <TopBar me={me} />
      <Outlet />
    </div>
  )
}
