import { createFileRoute } from '@tanstack/react-router'

export const Route = createFileRoute('/auth/error')({
  validateSearch: (search) => ({
    code: typeof search.code === 'string' ? search.code : 'auth_failed',
  }),
  component: AuthErrorRoute,
})

function AuthErrorRoute() {
  const { code } = Route.useSearch()
  return (
    <main style={{ maxWidth: 520, margin: '0 auto', padding: 16 }}>
      <h1 style={{ margin: 0 }}>We couldn’t sign you in</h1>
      <p style={{ opacity: 0.8 }}>
        Something went wrong while finishing authentication ({code}).
      </p>
      <a href="/login" style={{ fontWeight: 700 }}>
        Back to sign in
      </a>
    </main>
  )
}
