import { createFileRoute } from '@tanstack/react-router'
import { LoginScreen } from '../components/LoginScreen'

export const Route = createFileRoute('/login')({
  validateSearch: (search) => ({
    returnTo: typeof search.returnTo === 'string' ? search.returnTo : '/',
  }),
  component: LoginRoute,
})

function LoginRoute() {
  const { returnTo } = Route.useSearch()
  return <LoginScreen returnTo={returnTo} />
}
