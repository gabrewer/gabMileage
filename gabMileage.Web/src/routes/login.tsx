import { createFileRoute } from '@tanstack/react-router'
import { LoginScreen } from '../components/LoginScreen'
import { sanitizeReturnTo } from '../lib/returnTo'

export const Route = createFileRoute('/login')({
  validateSearch: (search) => ({
    returnTo: sanitizeReturnTo(search.returnTo),
  }),
  component: LoginRoute,
})

function LoginRoute() {
  const { returnTo } = Route.useSearch()
  return <LoginScreen returnTo={returnTo} />
}
