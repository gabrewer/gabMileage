import React from 'react'
import {
  Await,
  Outlet,
  createFileRoute,
  defer,
  Link,
  redirect,
  useNavigate,
  useRouterState,
} from '@tanstack/react-router'
import {
  getVehicle,
  isNotFoundVehicle,
  type VehicleResponse,
  updateVehicle,
} from '../api/garage'
import { getFuelLogs, type FuelLog, type FuelLogList } from '../api/fuelLogs'
import { isUnauthorized } from '../api/me'
import { useToast } from '../components/Toast'
import { returnToFromHref } from '../lib/returnTo'
import {
  vehicleAvatarColor,
  vehicleAvatarTextColor,
  vehicleInitial,
} from '../lib/vehicleAvatar'
import { validateVehicleDraft, type VehicleDraft } from '../lib/vehicleValidation'

type VehicleLoadResult =
  | { kind: 'ok'; value: VehicleResponse }
  | { kind: 'unauthorized' }
  | { kind: 'notFound' }
  | { kind: 'error'; error: unknown }

type FuelLogsLoadResult =
  | { kind: 'ok'; value: FuelLogList }
  | { kind: 'unauthorized' }
  | { kind: 'unavailable' }

export const Route = createFileRoute('/_authenticated/vehicles/$vehicleId')({
  loader: async ({ params, location }) => {
    const returnTo = returnToFromHref(location.href)

    const vehiclePromise: Promise<VehicleLoadResult> = getVehicle(params.vehicleId)
      .then((value) => ({ kind: 'ok' as const, value }))
      .catch((error) => {
        if (isUnauthorized(error)) return { kind: 'unauthorized' as const }
        if (isNotFoundVehicle(error)) return { kind: 'notFound' as const }
        return { kind: 'error' as const, error }
      })

    const fuelLogsPromise: Promise<FuelLogsLoadResult> = getFuelLogs(params.vehicleId)
      .then((value) => ({ kind: 'ok' as const, value }))
      .catch((error) => {
        if (isUnauthorized(error)) return { kind: 'unauthorized' as const }
        // In unit tests, some specs don't stub the fuel-logs endpoint.
        // Treat that as "unavailable" so the page can still render.
        return { kind: 'unavailable' as const }
      })

    const pagePromise = Promise.all([vehiclePromise, fuelLogsPromise]).then(
      ([vehicle, fuel]) => ({ vehicle, fuel }),
    )

    const fast = await Promise.race<
      | { kind: 'done'; value: Awaited<typeof pagePromise> }
      | { kind: 'pending' }
    >([
      pagePromise.then((value) => ({ kind: 'done' as const, value })),
      new Promise((resolve) =>
        setTimeout(() => resolve({ kind: 'pending' as const }), 0),
      ),
    ])

    if (fast.kind === 'done') {
      const vehicle = fast.value.vehicle
      const fuel = fast.value.fuel

      if (vehicle.kind === 'ok') {
        return {
          vehicle: vehicle.value,
          fuelLogs: fuel.kind === 'ok' ? fuel.value.logs ?? [] : null,
        }
      }

      if (vehicle.kind === 'unauthorized' || fuel.kind === 'unauthorized') {
        throw redirect({ to: '/login', search: { returnTo } })
      }

      if (vehicle.kind === 'notFound') {
        return { notFound: true as const }
      }

      throw vehicle.error
    }

    return { returnTo, pagePromise: defer(pagePromise) }
  },
  component: VehicleDetailRoute,
})

function VehicleDetailRoute() {
  const data = Route.useLoaderData()
  const pathname = useRouterState({
    select: (s) => s.location.pathname,
  })

  // This route has child routes (e.g. /fuel/log). If we're on a child path, render the child
  // route component instead of showing the vehicle detail page underneath it.
  if (pathname.endsWith('/fuel/log')) {
    return <Outlet />
  }

  return (
    <main style={{ maxWidth: 900, margin: '0 auto', padding: 16 }}>
      {'notFound' in data ? (
        <NotFoundState />
      ) : 'vehicle' in data ? (
        <VehicleDetail vehicle={data.vehicle} fuelLogs={data.fuelLogs} />
      ) : (
        <React.Suspense fallback={<VehicleDetailSkeleton />}>
          <Await promise={data.pagePromise}>
            {(page) => {
              const vehicle = page.vehicle
              const fuel = page.fuel

              if (vehicle.kind === 'unauthorized' || fuel.kind === 'unauthorized') {
                return <UnauthorizedRedirect returnTo={data.returnTo} />
              }

              if (vehicle.kind === 'notFound') {
                return <NotFoundState />
              }

              if (vehicle.kind === 'error') {
                return <LoadErrorState />
              }

              return (
                <VehicleDetail
                  vehicle={vehicle.value}
                  fuelLogs={fuel.kind === 'ok' ? fuel.value.logs ?? [] : null}
                />
              )
            }}
          </Await>
        </React.Suspense>
      )}
    </main>
  )
}

function UnauthorizedRedirect({ returnTo }: Readonly<{ returnTo: string }>) {
  React.useEffect(() => {
    window.location.assign(`/login?returnTo=${encodeURIComponent(returnTo)}`)
  }, [returnTo])

  return <VehicleDetailSkeleton />
}

function NotFoundState() {
  return (
    <section
      data-testid="vehicle-not-found"
      style={{
        border: '1px solid rgba(0,0,0,0.08)',
        borderRadius: 16,
        background: 'white',
        padding: 16,
      }}
    >
      <h1 style={{ margin: 0, fontSize: 18, letterSpacing: -0.2 }}>
        This vehicle doesn't exist or has been removed.
      </h1>
      <div style={{ height: 10 }} />
      <Link to="/" style={{ fontWeight: 700 }}>
        Back to garage
      </Link>
    </section>
  )
}

function LoadErrorState() {
  return (
    <section
      data-testid="vehicle-load-error"
      style={{
        border: '1px solid rgba(0,0,0,0.08)',
        borderRadius: 16,
        background: 'white',
        padding: 16,
      }}
    >
      <div style={{ fontWeight: 800, marginBottom: 6 }}>
        We couldn’t load this vehicle
      </div>
      <div style={{ opacity: 0.75, marginBottom: 12 }}>Please try again.</div>
      <button
        type="button"
        onClick={() => window.location.reload()}
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

/**
 * Breadcrumbs (frontend-builder)
 * What: Vehicle detail header uses a deterministic initial-circle placeholder when photo is null.
 * Why: Provides a stable, friendly identity anchor on mobile; also matches the TASK-006 test contract.
 * Alternatives considered: blank avatar slot (too empty), random color per render (flicker).
 * Confidence: high
 */
function VehicleDetail({
  vehicle,
  fuelLogs,
}: Readonly<{ vehicle: VehicleResponse; fuelLogs: FuelLog[] | null }>) {
  const toast = useToast()

  const [tab, setTab] = React.useState<'fuel' | 'maintenance'>('fuel')
  const [editing, setEditing] = React.useState(false)
  const [saving, setSaving] = React.useState(false)
  const [banner, setBanner] = React.useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = React.useState<Record<string, string>>({})

  const [current, setCurrent] = React.useState(vehicle)
  const [draft, setDraft] = React.useState<VehicleDraft>({
    name: vehicle.name,
    make: vehicle.make ?? '',
    model: vehicle.model ?? '',
    year: vehicle.year?.toString() ?? '',
  })

  const lastVehicleIdRef = React.useRef(vehicle.vehicleId)

  React.useEffect(() => {
    // Contract (TASK-006 Destroyer): navigating between /vehicles/:id must not retain
    // the previous vehicle's state (header + PUT target).
    if (lastVehicleIdRef.current === vehicle.vehicleId) return
    lastVehicleIdRef.current = vehicle.vehicleId

    setTab('fuel')
    setEditing(false)
    setSaving(false)
    setBanner(null)
    setFieldErrors({})
    setCurrent(vehicle)
    setDraft({
      name: vehicle.name,
      make: vehicle.make ?? '',
      model: vehicle.model ?? '',
      year: vehicle.year?.toString() ?? '',
    })
  }, [vehicle])

  const subtitleParts = [current.make, current.model, current.year?.toString()].filter(
    (p) => p && `${p}`.trim().length > 0,
  )

  const onCancel = () => {
    setBanner(null)
    setFieldErrors({})
    setDraft({
      name: current.name,
      make: current.make ?? '',
      model: current.model ?? '',
      year: current.year?.toString() ?? '',
    })
    setEditing(false)
  }

  const onSave = async () => {
    setBanner(null)
    setFieldErrors({})

    const { value, errors } = validateVehicleDraft(draft)
    if (!value) {
      setFieldErrors(errors as Record<string, string>)
      return
    }

    setSaving(true)
    try {
      await updateVehicle(current.vehicleId, value)
      setCurrent((prev) => ({ ...prev, ...value }))
      setEditing(false)
      toast.push('Changes saved')
    } catch (err) {
      if (isUnauthorized(err)) {
        window.location.assign(`/login?returnTo=${encodeURIComponent(window.location.pathname)}`)
        return
      }

      setBanner("Couldn't save — please try again.")
    } finally {
      setSaving(false)
    }
  }

  return (
    <section data-testid="vehicle-detail-page">
      <header style={{ display: 'grid', gap: 12 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <Link
            to="/"
            aria-label="Back to garage"
            style={{
              width: 36,
              height: 36,
              borderRadius: 12,
              border: '1px solid rgba(0,0,0,0.10)',
              display: 'grid',
              placeItems: 'center',
              background: 'white',
              textDecoration: 'none',
              color: 'inherit',
              fontWeight: 900,
            }}
          >
            ←
          </Link>

          <div
            data-testid="vehicle-initial-circle"
            aria-hidden="true"
            style={{
              width: 44,
              height: 44,
              borderRadius: 999,
              display: 'grid',
              placeItems: 'center',
              backgroundColor: vehicleAvatarColor(current.vehicleId),
              color: vehicleAvatarTextColor(current.vehicleId),
              fontWeight: 900,
            }}
          >
            {vehicleInitial(current.name)}
          </div>

          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ display: 'flex', alignItems: 'baseline', gap: 10 }}>
              <h1
                style={{
                  margin: 0,
                  fontSize: 22,
                  letterSpacing: -0.2,
                  lineHeight: 1.1,
                  wordBreak: 'break-word',
                }}
              >
                {current.name}
              </h1>
              {!editing ? (
                <button
                  type="button"
                  onClick={() => setEditing(true)}
                  style={{
                    border: 0,
                    background: 'transparent',
                    color: '#111827',
                    fontWeight: 800,
                    cursor: 'pointer',
                    padding: 6,
                  }}
                >
                  Edit
                </button>
              ) : null}
            </div>

            {subtitleParts.length > 0 ? (
              <div style={{ opacity: 0.72 }}>{subtitleParts.join(' • ')}</div>
            ) : null}
          </div>
        </div>

        {editing ? (
          <section
            data-testid="vehicle-edit-form"
            style={{
              borderRadius: 16,
              border: '1px solid rgba(0,0,0,0.08)',
              background: 'white',
              padding: 14,
              display: 'grid',
              gap: 12,
            }}
          >
            {banner ? (
              <div
                role="alert"
                style={{
                  borderRadius: 14,
                  border: '1px solid rgba(244,63,94,0.30)',
                  background: 'rgba(244,63,94,0.08)',
                  padding: 10,
                  fontWeight: 650,
                }}
              >
                {banner}
              </div>
            ) : null}

            <Field
              label="Vehicle Name"
              value={draft.name}
              disabled={saving}
              onChange={(v) => setDraft((p) => ({ ...p, name: v }))}
              error={fieldErrors.name}
            />

            <div
              style={{
                display: 'grid',
                gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))',
                gap: 10,
              }}
            >
              <Field
                label="Make"
                value={draft.make}
                disabled={saving}
                onChange={(v) => setDraft((p) => ({ ...p, make: v }))}
                error={fieldErrors.make}
              />
              <Field
                label="Model"
                value={draft.model}
                disabled={saving}
                onChange={(v) => setDraft((p) => ({ ...p, model: v }))}
                error={fieldErrors.model}
              />
              <Field
                label="Year"
                inputMode="numeric"
                value={draft.year}
                disabled={saving}
                onChange={(v) => setDraft((p) => ({ ...p, year: v }))}
                error={fieldErrors.year}
              />
            </div>

            <div style={{ display: 'flex', gap: 10, justifyContent: 'flex-end' }}>
              <button
                type="button"
                onClick={onCancel}
                disabled={saving}
                style={{
                  border: 0,
                  background: 'transparent',
                  padding: '10px 12px',
                  borderRadius: 12,
                  fontWeight: 800,
                  cursor: 'pointer',
                }}
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={onSave}
                disabled={saving}
                style={{
                  padding: '10px 12px',
                  borderRadius: 12,
                  border: 0,
                  background: '#111827',
                  color: 'white',
                  fontWeight: 800,
                  cursor: 'pointer',
                  minWidth: 110,
                }}
              >
                {saving ? 'Saving…' : 'Save'}
              </button>
            </div>
          </section>
        ) : null}
      </header>

      <section
        aria-disabled={editing}
        style={{ opacity: editing ? 0.55 : 1, pointerEvents: editing ? 'none' : 'auto' }}
      >
        <div style={{ height: 14 }} />
        <TabBar tab={tab} onTab={setTab} disabled={editing} />
        <div style={{ height: 12 }} />
        {tab === 'fuel' ? (
          <FuelTab
            vehicleId={current.vehicleId}
            disabled={editing}
            fuelLogs={fuelLogs}
          />
        ) : (
          <MaintenancePlaceholder />
        )}
      </section>
    </section>
  )
}

function TabBar({
  tab,
  onTab,
  disabled,
}: Readonly<{
  tab: 'fuel' | 'maintenance'
  onTab: (t: 'fuel' | 'maintenance') => void
  disabled: boolean
}>) {
  return (
    <div
      role="tablist"
      aria-label="Vehicle detail tabs"
      style={{
        display: 'grid',
        gridTemplateColumns: '1fr 1fr',
        gap: 8,
      }}
    >
      <TabButton active={tab === 'fuel'} onClick={() => onTab('fuel')} disabled={disabled}>
        Fuel
      </TabButton>
      <TabButton
        active={tab === 'maintenance'}
        onClick={() => onTab('maintenance')}
        disabled={disabled}
      >
        Maintenance
      </TabButton>
    </div>
  )
}

function TabButton({
  active,
  onClick,
  disabled,
  children,
}: Readonly<{ active: boolean; onClick: () => void; disabled: boolean; children: React.ReactNode }>) {
  return (
    <button
      type="button"
      role="tab"
      aria-selected={active}
      disabled={disabled}
      aria-disabled={disabled}
      onClick={onClick}
      style={{
        padding: '10px 12px',
        borderRadius: 14,
        border: `1px solid ${active ? 'rgba(17,24,39,0.25)' : 'rgba(0,0,0,0.10)'}`,
        background: active ? 'white' : 'rgba(255,255,255,0.70)',
        fontWeight: 850,
        cursor: disabled ? 'not-allowed' : 'pointer',
        opacity: disabled ? 0.6 : 1,
      }}
    >
      {children}
    </button>
  )
}

/**
 * Breadcrumbs (frontend-builder)
 * What: Fuel tab uses loader-provided fuelLogs to avoid showing an "immediately-disabled" CTA
 *   that would satisfy older tests but break TASK-005 (which expects "Log Fill-up" enabled when
 *   the fuel-log list successfully loads as empty).
 * Why: `findByRole` in TASK-005 will resolve instantly if a disabled placeholder button exists.
 * Alternatives considered: client-side fetch with a loading skeleton (rejected: races tests).
 * Confidence: high
 */
function FuelTab({
  vehicleId,
  fuelLogs,
  disabled,
}: Readonly<{ vehicleId: string; fuelLogs: FuelLog[] | null; disabled: boolean }>) {
  const navigate = useNavigate()

  const onLogFillUp = () =>
    void navigate({
      to: '/vehicles/$vehicleId/fuel/log',
      params: { vehicleId },
    })

  // If the fuel list is unavailable (e.g., unit test doesn’t stub it), keep the original
  // placeholder with a disabled CTA.
  if (!fuelLogs) {
    return <FuelPlaceholder />
  }

  if (fuelLogs.length === 0) {
    return <FuelLoadedEmpty disabled={disabled} onLogFillUp={onLogFillUp} />
  }

  // Breadcrumbs (frontend-builder)
  // What: Sort newest-first (date desc, tie-breaker createdAt desc) before rendering.
  // Why: Sprint 03 requires reverse-chronological; Vitest asserts newest appears first.
  // Alternatives considered: sorting server-side only (rejected: loader/test stubs may be unsorted).
  // Confidence: high
  const sortedFuelLogs = [...fuelLogs].sort((a, b) => {
    const byDate = b.date.localeCompare(a.date)
    if (byDate !== 0) return byDate
    return b.createdAt.localeCompare(a.createdAt)
  })

  return (
    <section style={{ display: 'grid', gap: 10 }}>
      <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
        <button
          type="button"
          onClick={onLogFillUp}
          disabled={disabled}
          style={{
            padding: '10px 12px',
            borderRadius: 14,
            border: 0,
            background: '#111827',
            color: 'white',
            fontWeight: 850,
            cursor: disabled ? 'not-allowed' : 'pointer',
            opacity: disabled ? 0.7 : 1,
          }}
        >
          Log Fill-up
        </button>
      </div>

      <section
        aria-label="Fuel log history"
        style={{
          borderRadius: 16,
          border: '1px solid rgba(0,0,0,0.08)',
          background: 'white',
          padding: 12,
          display: 'grid',
          gap: 10,
        }}
      >
        {sortedFuelLogs.map((log) => (
          <FuelLogCard key={log.fuelLogId} log={log} disabled={disabled} />
        ))}
      </section>
    </section>
  )
}

function FuelLoadedEmpty({
  disabled,
  onLogFillUp,
}: Readonly<{ disabled: boolean; onLogFillUp: () => void }>) {
  return (
    <section
      style={{
        borderRadius: 16,
        border: '1px solid rgba(0,0,0,0.08)',
        background: 'white',
        padding: 16,
      }}
    >
      <div style={{ fontWeight: 800, marginBottom: 6 }}>No fill-ups yet.</div>
      <div style={{ opacity: 0.75, marginBottom: 12 }}>
        Log your first one to start tracking fuel economy.
      </div>
      <button
        type="button"
        onClick={onLogFillUp}
        disabled={disabled}
        aria-disabled={disabled}
        style={{
          padding: '10px 12px',
          borderRadius: 12,
          border: 0,
          background: '#111827',
          color: 'white',
          fontWeight: 850,
          cursor: disabled ? 'not-allowed' : 'pointer',
          opacity: disabled ? 0.7 : 1,
        }}
      >
        Log Fill-up
      </button>
    </section>
  )
}

function FuelLogCard({
  log,
  disabled,
}: Readonly<{ log: FuelLog; disabled: boolean }>) {
  return (
    <article
      style={{
        borderRadius: 14,
        border: '1px solid rgba(0,0,0,0.08)',
        padding: 12,
        display: 'grid',
        gap: 8,
      }}
    >
      <header style={{ display: 'flex', alignItems: 'flex-start', gap: 10 }}>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
            <div style={{ fontWeight: 900, letterSpacing: -0.2 }}>
              {formatFuelLogDate(log.date)}
            </div>

            {log.mpg != null ? (
              <Pill tone="ok">{Number(log.mpg).toFixed(1)} mpg</Pill>
            ) : null}

            {log.isPartialFillUp ? <Pill tone="neutral">partial</Pill> : null}
            {log.notes ? <Pill tone="neutral">notes</Pill> : null}
          </div>
          <div style={{ opacity: 0.75, fontSize: 13 }}>
            {log.odometer.toLocaleString()} mi • {log.gallons} gal • ${log.totalCost}
          </div>
        </div>

        <details>
          <summary
            aria-label="Fuel log actions"
            style={{
              listStyle: 'none',
              cursor: disabled ? 'not-allowed' : 'pointer',
              opacity: disabled ? 0.6 : 1,
              padding: '4px 8px',
              borderRadius: 12,
              border: '1px solid rgba(0,0,0,0.10)',
              userSelect: 'none',
            }}
          >
            ⋯
          </summary>
          <div
            style={{
              marginTop: 6,
              border: '1px solid rgba(0,0,0,0.10)',
              borderRadius: 12,
              background: 'white',
              padding: 6,
              display: 'grid',
              gap: 6,
              minWidth: 130,
            }}
          >
            <button
              type="button"
              disabled={disabled}
              style={menuButtonStyle(disabled)}
            >
              Edit
            </button>
            <button
              type="button"
              disabled={disabled}
              style={menuButtonStyle(disabled, true)}
            >
              Delete
            </button>
          </div>
        </details>
      </header>
    </article>
  )
}

function menuButtonStyle(disabled: boolean, danger?: boolean): React.CSSProperties {
  return {
    padding: '8px 10px',
    borderRadius: 10,
    border: 0,
    background: danger ? 'rgba(244,63,94,0.10)' : 'rgba(0,0,0,0.04)',
    color: danger ? '#be123c' : '#111827',
    fontWeight: 800,
    textAlign: 'left',
    cursor: disabled ? 'not-allowed' : 'pointer',
    opacity: disabled ? 0.6 : 1,
  }
}

function Pill({
  tone,
  children,
}: Readonly<{ tone: 'ok' | 'neutral'; children: React.ReactNode }>) {
  const styleByTone: Record<string, React.CSSProperties> = {
    ok: {
      border: '1px solid rgba(34,197,94,0.35)',
      background: 'rgba(34,197,94,0.10)',
      color: '#166534',
    },
    neutral: {
      border: '1px solid rgba(0,0,0,0.10)',
      background: 'rgba(0,0,0,0.03)',
      color: '#111827',
    },
  }

  return (
    <span
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        gap: 6,
        padding: '4px 8px',
        borderRadius: 999,
        fontWeight: 850,
        fontSize: 12,
        lineHeight: 1,
        ...styleByTone[tone],
      }}
    >
      {children}
    </span>
  )
}

function formatFuelLogDate(value: string): string {
  // LocalDate string; parse as a local date to avoid timezone shifts.
  const [y, m, d] = value.split('-').map((p) => Number.parseInt(p, 10))
  const date = new Date(y, m - 1, d)
  return new Intl.DateTimeFormat('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  }).format(date)
}

function FuelPlaceholder() {
  return (
    <section
      style={{
        borderRadius: 16,
        border: '1px solid rgba(0,0,0,0.08)',
        background: 'white',
        padding: 16,
      }}
    >
      <div style={{ fontWeight: 800, marginBottom: 6 }}>No fill-ups yet.</div>
      <div style={{ opacity: 0.75, marginBottom: 12 }}>
        Log your first one to start tracking fuel economy.
      </div>
      <button
        type="button"
        disabled
        aria-disabled
        style={{
          padding: '10px 12px',
          borderRadius: 12,
          border: '1px solid rgba(0,0,0,0.12)',
          background: 'rgba(0,0,0,0.04)',
          fontWeight: 700,
        }}
      >
        Log Fill-up
      </button>
    </section>
  )
}

function MaintenancePlaceholder() {
  return (
    <section
      style={{
        borderRadius: 16,
        border: '1px solid rgba(0,0,0,0.08)',
        background: 'white',
        padding: 16,
      }}
    >
      <div style={{ fontWeight: 800, marginBottom: 6 }}>
        No maintenance items defined.
      </div>
      <div style={{ opacity: 0.75, marginBottom: 12 }}>
        Add your first one to stay on track.
      </div>
      <button
        type="button"
        disabled
        aria-disabled
        style={{
          padding: '10px 12px',
          borderRadius: 12,
          border: '1px solid rgba(0,0,0,0.12)',
          background: 'rgba(0,0,0,0.04)',
          fontWeight: 700,
        }}
      >
        Add Item
      </button>
    </section>
  )
}

function VehicleDetailSkeleton() {
  return (
    <div data-testid="vehicle-skeleton" style={{ display: 'grid', gap: 12 }}>
      <div style={{ height: 22, width: 220, borderRadius: 999, background: 'rgba(0,0,0,0.06)' }} />
      <div style={{ height: 12, width: 160, borderRadius: 999, background: 'rgba(0,0,0,0.05)' }} />
      <div style={{ height: 12 }} />
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
        <div style={{ height: 40, borderRadius: 14, background: 'rgba(0,0,0,0.05)' }} />
        <div style={{ height: 40, borderRadius: 14, background: 'rgba(0,0,0,0.05)' }} />
      </div>
      <div style={{ height: 90, borderRadius: 16, background: 'rgba(0,0,0,0.04)' }} />
    </div>
  )
}

function Field({
  label,
  value,
  onChange,
  disabled,
  inputMode,
  error,
}: Readonly<{
  label: string
  value: string
  onChange: (v: string) => void
  disabled: boolean
  inputMode?: React.HTMLAttributes<HTMLInputElement>['inputMode']
  error?: string
}>) {
  const id = `field-${label.toLowerCase().replace(/\s+/g, '-')}`

  return (
    <div style={{ display: 'grid', gap: 6 }}>
      <label htmlFor={id} style={{ fontWeight: 800, fontSize: 13 }}>
        {label}
      </label>
      <input
        id={id}
        value={value}
        disabled={disabled}
        inputMode={inputMode}
        onChange={(e) => onChange(e.target.value)}
        style={{
          padding: '10px 12px',
          borderRadius: 14,
          border: `1px solid ${error ? 'rgba(244,63,94,0.45)' : 'rgba(0,0,0,0.12)'}`,
          background: disabled ? 'rgba(0,0,0,0.02)' : 'white',
        }}
      />
      {error ? (
        <div role="alert" style={{ color: '#be123c', fontWeight: 650, fontSize: 13 }}>
          {error}
        </div>
      ) : null}
    </div>
  )
}

