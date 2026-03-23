import React from 'react'
import { Await, createFileRoute, defer, useNavigate } from '@tanstack/react-router'
import { getGarageSummary, type GarageSummaryResponse } from '../api/garage'
import { DashboardEmptyState } from '../components/DashboardEmptyState'
import { DashboardSkeleton } from '../components/DashboardSkeleton'

export const Route = createFileRoute('/_authenticated/')({
  loader: async () => {
    const garagePromise = getGarageSummary()

    // If the garage request resolves immediately, return it as loader data so
    // tests (and the UI) can render the correct state without showing a skeleton.
    // If it doesn't, defer it so navigation can complete and a skeleton can render.
    const fast = await Promise.race<
      | { kind: 'done'; value: GarageSummaryResponse }
      | { kind: 'pending' }
    >([
      garagePromise.then((value) => ({ kind: 'done' as const, value })),
      new Promise((resolve) => setTimeout(() => resolve({ kind: 'pending' as const }), 0)),
    ])

    if (fast.kind === 'done') {
      return { garage: fast.value }
    }

    return { garagePromise: defer(garagePromise) }
  },
  component: DashboardRoute,
})

function DashboardRoute() {
  const navigate = useNavigate()
  const data = Route.useLoaderData()

  const onAddVehicle = () => void navigate({ to: '/vehicles/new' })

  return (
    <main style={{ maxWidth: 1100, margin: '0 auto', padding: 16 }}>
      <section style={{ marginBottom: 14 }}>
        <h1 style={{ margin: 0, fontSize: 18, letterSpacing: -0.2 }}>
          Garage
        </h1>
        <p style={{ margin: '6px 0 0', opacity: 0.75 }}>
          Quick glance, gentle reminders.
        </p>
      </section>

      {'garage' in data ? (
        <DashboardBody vehicles={data.garage.vehicles} onAddVehicle={onAddVehicle} />
      ) : (
        <React.Suspense fallback={<DashboardSkeleton />}>
          <Await promise={data.garagePromise}>
            {(garageData) => (
              <DashboardBody vehicles={garageData.vehicles} onAddVehicle={onAddVehicle} />
            )}
          </Await>
        </React.Suspense>
      )}
    </main>
  )
}

function DashboardBody({
  vehicles,
  onAddVehicle,
}: Readonly<{ vehicles: unknown[]; onAddVehicle: () => void }>) {
  // Sprint 01 contract: vehicles is always empty; still implement list branch for extend-only evolution.
  if (!vehicles || vehicles.length === 0) {
    return <DashboardEmptyState onAddVehicle={onAddVehicle} />
  }

  return (
    <section
      data-testid="garage-vehicle-list"
      style={{
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
        gap: 12,
      }}
    >
      {vehicles.map((_v, idx) => (
        // eslint-disable-next-line react/no-array-index-key
        <div
          key={idx}
          style={{
            border: '1px solid rgba(0,0,0,0.08)',
            borderRadius: 16,
            padding: 14,
            background: 'white',
          }}
        >
          <div style={{ fontWeight: 800 }}>Vehicle</div>
          <div style={{ opacity: 0.7, fontSize: 12 }}>Details coming soon</div>
        </div>
      ))}
    </section>
  )
}
