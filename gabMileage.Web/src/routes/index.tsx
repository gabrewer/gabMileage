import { createFileRoute } from '@tanstack/react-router'

export const Route = createFileRoute('/')({
  component: Home,
})

function Home() {
  return (
    <main style={{ fontFamily: 'system-ui, sans-serif', padding: 16 }}>
      <h1>gabMileage</h1>
      <p>TanStack Start scaffold.</p>
      <p>
        <a href="/health">/health</a>
      </p>
    </main>
  )
}
