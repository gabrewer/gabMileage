import React from 'react'
import {
  Await,
  createFileRoute,
  defer,
  redirect,
  useNavigate,
} from '@tanstack/react-router'
import { getGarageSummary, type GarageSummaryResponse } from '../api/garage'
import { isUnauthorized } from '../api/me'
import { DashboardEmptyState } from '../components/DashboardEmptyState'
import { DashboardSkeleton } from '../components/DashboardSkeleton'
import { returnToFromHref } from '../lib/returnTo'

type GarageLoadResult =
  | { kind: 'ok'; value: GarageSummaryResponse }
  | { kind: 'unauthorized' }
  | { kind: 'error'; error: unknown }

export const Route = createFileRoute('/_authenticated/')({
  loader: async ({ location }) => {
    const returnTo = returnToFromHref(location.href)

    const garagePromise: Promise<GarageLoadResult> = getGarageSummary()
      .then((value) => ({ kind: 'ok' as const, value }))
      .catch((error) => {
        if (isUnauthorized(error)) return { kind: 'unauthorized' as const }
        return { kind: 'error' as const, error }
      })

    // If the garage request resolves immediately, return it as loader data so
    // tests (and the UI) can render the correct state without showing a skeleton.
    // If it doesn't, defer it so navigation can complete and a skeleton can render.
    const fast = await Promise.race<
      | { kind: 'done'; value: GarageLoadResult }
      | { kind: 'pending' }
    >([
      garagePromise.then((value) => ({ kind: 'done' as const, value })),
      new Promise((resolve) =>
        setTimeout(() => resolve({ kind: 'pending' as const }), 0),
      ),
    ])

    if (fast.kind === 'done') {
      if (fast.value.kind === 'ok') return { garage: fast.value.value }

      if (fast.value.kind === 'unauthorized') {
        throw redirect({
          to: '/login',
          search: { returnTo },
        })
      }

      throw fast.value.error
    }

    return { returnTo, garagePromise: defer(garagePromise) }
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
            {(result) => {
              if (result.kind === 'unauthorized') {
                return <UnauthorizedRedirect returnTo={data.returnTo} />
              }

              if (result.kind === 'error') {
                return <DashboardLoadErrorState onRetry={() => window.location.reload()} />
              }

              return (
                <DashboardBody
                  vehicles={result.value.vehicles}
                  onAddVehicle={onAddVehicle}
                />
              )
            }}
          </Await>
        </React.Suspense>
      )}
    </main>
  )
}

function UnauthorizedRedirect({
  returnTo,
}: Readonly<{ returnTo: string }>) {
  const navigate = useNavigate()

  React.useEffect(() => {
    void navigate({ to: '/login', search: { returnTo } })
  }, [navigate, returnTo])

  return <DashboardSkeleton />
}

function DashboardLoadErrorState({
  onRetry,
}: Readonly<{ onRetry: () => void }>) {
  return (
    <section
      data-testid="garage-load-error"
      style={{
        border: '1px solid rgba(0,0,0,0.08)',
        borderRadius: 16,
        background: 'white',
        padding: 16,
      }}
    >
      <div style={{ fontWeight: 800, marginBottom: 6 }}>
        We couldn’t load your garage
      </div>
      <div style={{ opacity: 0.75, marginBottom: 12 }}>
        Please try again.
      </div>
      <button
        type="button"
        onClick={onRetry}
        style={{
          padding: '10px 12px',
          borderRadius: 12,
          border: '1px solid rgba(0,0,0,0.12)',
          background: 'white',
          fontWeight: 700,
          cursor: 'pointer',
        }}
      >
        Retry
      </button>
    </section>
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
