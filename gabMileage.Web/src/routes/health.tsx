import { createFileRoute } from '@tanstack/react-router'

// Aspire AppHost uses this for liveness checks.
export const Route = createFileRoute('/health')({
  component: Health,
})

function Health() {
  return <pre>ok</pre>
}
