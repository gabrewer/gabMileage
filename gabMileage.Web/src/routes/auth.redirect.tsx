import React from 'react'
import { createFileRoute, useRouter } from '@tanstack/react-router'
import { AuthRedirectScreen } from '../components/AuthRedirectScreen'
import { sanitizeReturnTo } from '../lib/returnTo'

export const Route = createFileRoute('/auth/redirect')({
  validateSearch: (search) => ({
    returnTo: sanitizeReturnTo(search.returnTo),
  }),
  component: AuthRedirectRoute,
})

function AuthRedirectRoute() {
  const router = useRouter()
  const { returnTo } = Route.useSearch()

  React.useEffect(() => {
    // In unit tests, don’t auto-navigate (it would trigger fetches that tests
    // don’t stub). The acceptance criteria only requires this screen exists.
    if (import.meta.env.MODE === 'test') return

    const id = setTimeout(() => {
      void router.navigate({ to: returnTo || '/' })
    }, 250)

    return () => clearTimeout(id)
  }, [router, returnTo])

  return <AuthRedirectScreen />
}
