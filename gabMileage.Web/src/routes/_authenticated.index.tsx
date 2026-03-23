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
import { vehicleAvatarColor, vehicleAvatarTextColor, vehicleInitial } from '../lib/vehicleAvatar'

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

      // Contract (TASK-004 Destroyer): even an immediate failure must render the in-page
      // retryable error state (not escape via a thrown loader error boundary).
      return { loadError: true as const }
    }

    return { returnTo, garagePromise: defer(garagePromise) }
  },
  component: DashboardRoute,
})

function DashboardRoute() {
  const navigate = useNavigate()
  const data = Route.useLoaderData()

  // Avoid duplicate "+ Add Vehicle" CTAs when the empty state is visible.
  // This must be synchronous on first render so tests don't observe a double-CTA flash.
  const [hideHeaderCta, setHideHeaderCta] = React.useState(() =>
    'garage' in data ? !data.garage.vehicles || data.garage.vehicles.length === 0 : false,
  )

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
          <h1 style={{ margin: 0, fontSize: 18, letterSpacing: -0.2 }}>Your Garage</h1>
          <p style={{ margin: '6px 0 0', opacity: 0.75 }}>
            Quick glance, gentle reminders.
          </p>
        </div>

        {!hideHeaderCta ? (
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
            + Add Vehicle
          </button>
        ) : null}
      </section>

      {'loadError' in data ? (
        <DashboardLoadErrorState onRetry={() => window.location.reload()} />
      ) : 'garage' in data ? (
        <DashboardBody
          vehicles={data.garage.vehicles}
          onAddVehicle={onAddVehicle}
          onOpenVehicle={onOpenVehicle}
          setHideHeaderCta={setHideHeaderCta}
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
                  setHideHeaderCta={setHideHeaderCta}
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
  setHideHeaderCta,
}: Readonly<{
  vehicles: GarageSummaryVehicle[]
  onAddVehicle: () => void
  onOpenVehicle: (vehicleId: string) => void
  setHideHeaderCta: (hidden: boolean) => void
}>) {
  const useIsoLayoutEffect =
    typeof window === 'undefined' ? React.useEffect : React.useLayoutEffect

  useIsoLayoutEffect(() => {
    setHideHeaderCta(!vehicles || vehicles.length === 0)
  }, [setHideHeaderCta, vehicles])
  if (!vehicles || vehicles.length === 0) {
    return <DashboardEmptyState onAddVehicle={onAddVehicle} />
  }

  // Contract (TASK-004): UI sorts newest-first even if API returns out-of-order.
  const sorted = [...vehicles].sort((a, b) => b.createdAt.localeCompare(a.createdAt))

  return (
    <section
      data-testid="garage-vehicle-list"
      style={{
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
        gap: 12,
      }}
    >
      {sorted.map((v) => {
        const subtitle = [v.make, v.model, v.year?.toString()]
          .filter((p) => p && `${p}`.trim().length > 0)
          .join(' • ')

        const initial = vehicleInitial(v.name)
        const bg = vehicleAvatarColor(v.vehicleId)
        const fg = vehicleAvatarTextColor(v.vehicleId)

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
              display: 'grid',
              gridTemplateColumns: '44px 1fr',
              gap: 12,
              alignItems: 'center',
            }}
          >
            <div
              data-testid={`vehicle-avatar-${v.vehicleId}`}
              aria-hidden="true"
              style={{
                width: 44,
                height: 44,
                borderRadius: 999,
                display: 'grid',
                placeItems: 'center',
                backgroundColor: bg,
                color: fg,
                fontWeight: 900,
              }}
            >
              {initial}
            </div>

            <div style={{ minWidth: 0 }}>
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
              {subtitle ? <div style={{ opacity: 0.72, fontSize: 13 }}>{subtitle}</div> : null}
            </div>
          </button>
        )
      })}
    </section>
  )
}
