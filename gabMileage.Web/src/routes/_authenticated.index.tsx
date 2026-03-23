import React from 'react'
import {
  Await,
  createFileRoute,
  defer,
  redirect,
  useNavigate,
} from '@tanstack/react-router'
import {
  getGarageSummary,
  type GarageSummaryResponse,
  type GarageSummaryVehicle,
} from '../api/garage'
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
  const onOpenVehicle = (vehicleId: string) =>
    void navigate({ to: '/vehicles/$vehicleId', params: { vehicleId } })

  return (
    <main style={{ maxWidth: 1100, margin: '0 auto', padding: 16 }}>
      <section
        style={{
          marginBottom: 14,
          display: 'flex',
          alignItems: 'flex-start',
          justifyContent: 'space-between',
          gap: 12,
        }}
      >
        <div>
          <h1 style={{ margin: 0, fontSize: 18, letterSpacing: -0.2 }}>Garage</h1>
          <p style={{ margin: '6px 0 0', opacity: 0.75 }}>
            Quick glance, gentle reminders.
          </p>
        </div>

        <button
          type="button"
          onClick={onAddVehicle}
          style={{
            padding: '10px 12px',
            borderRadius: 14,
            border: 0,
            background: '#111827',
            color: 'white',
            fontWeight: 850,
            cursor: 'pointer',
            whiteSpace: 'nowrap',
          }}
        >
          Add Vehicle
        </button>
      </section>

      {'garage' in data ? (
        <DashboardBody
          vehicles={data.garage.vehicles}
          onAddVehicle={onAddVehicle}
          onOpenVehicle={onOpenVehicle}
        />
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
                  onOpenVehicle={onOpenVehicle}
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
  onOpenVehicle,
}: Readonly<{
  vehicles: GarageSummaryVehicle[]
  onAddVehicle: () => void
  onOpenVehicle: (vehicleId: string) => void
}>) {
  if (!vehicles || vehicles.length === 0) {
    return <DashboardEmptyState onAddVehicle={onAddVehicle} />
  }

  return (
    <section
      data-testid="garage-vehicle-list"
      style={{
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
        gap: 12,
      }}
    >
      {vehicles.map((v) => {
        const subtitle = [v.make, v.model, v.year?.toString()]
          .filter((p) => p && `${p}`.trim().length > 0)
          .join(' • ')

        return (
          <button
            key={v.vehicleId}
            type="button"
            onClick={() => onOpenVehicle(v.vehicleId)}
            style={{
              textAlign: 'left',
              border: '1px solid rgba(0,0,0,0.08)',
              borderRadius: 16,
              padding: 14,
              background: 'white',
              cursor: 'pointer',
            }}
          >
            <div
              style={{
                fontWeight: 900,
                whiteSpace: 'nowrap',
                overflow: 'hidden',
                textOverflow: 'ellipsis',
              }}
              title={v.name}
            >
              {v.name}
            </div>
            {subtitle ? (
              <div style={{ opacity: 0.72, fontSize: 13 }}>{subtitle}</div>
            ) : (
              <div style={{ opacity: 0.55, fontSize: 13 }}>Add make/model/year</div>
            )}
          </button>
        )
      })}
    </section>
  )
}
