import React from 'react'
import { Await, createFileRoute, defer, Link, redirect } from '@tanstack/react-router'
import { getVehicle, isNotFoundVehicle, type VehicleResponse, updateVehicle } from '../api/garage'
import { isUnauthorized } from '../api/me'
import { useToast } from '../components/Toast'
import { returnToFromHref } from '../lib/returnTo'
import { vehicleAvatarColor, vehicleAvatarTextColor, vehicleInitial } from '../lib/vehicleAvatar'
import { validateVehicleDraft, type VehicleDraft } from '../lib/vehicleValidation'

type VehicleLoadResult =
  | { kind: 'ok'; value: VehicleResponse }
  | { kind: 'unauthorized' }
  | { kind: 'notFound' }
  | { kind: 'error'; error: unknown }

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

    const fast = await Promise.race<
      | { kind: 'done'; value: VehicleLoadResult }
      | { kind: 'pending' }
    >([
      vehiclePromise.then((value) => ({ kind: 'done' as const, value })),
      new Promise((resolve) =>
        setTimeout(() => resolve({ kind: 'pending' as const }), 0),
      ),
    ])

    if (fast.kind === 'done') {
      if (fast.value.kind === 'ok') return { vehicle: fast.value.value }

      if (fast.value.kind === 'unauthorized') {
        throw redirect({ to: '/login', search: { returnTo } })
      }

      if (fast.value.kind === 'notFound') {
        return { notFound: true as const }
      }

      throw fast.value.error
    }

    return { returnTo, vehiclePromise: defer(vehiclePromise) }
  },
  component: VehicleDetailRoute,
})

function VehicleDetailRoute() {
  const data = Route.useLoaderData()

  return (
    <main style={{ maxWidth: 900, margin: '0 auto', padding: 16 }}>
      {'notFound' in data ? (
        <NotFoundState />
      ) : 'vehicle' in data ? (
        <VehicleDetail vehicle={data.vehicle} />
      ) : (
        <React.Suspense fallback={<VehicleDetailSkeleton />}>
          <Await promise={data.vehiclePromise}>
            {(result) => {
              if (result.kind === 'unauthorized') {
                return <UnauthorizedRedirect returnTo={data.returnTo} />
              }

              if (result.kind === 'notFound') {
                return <NotFoundState />
              }

              if (result.kind === 'error') {
                return <LoadErrorState />
              }

              return <VehicleDetail vehicle={result.value} />
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
function VehicleDetail({ vehicle }: Readonly<{ vehicle: VehicleResponse }>) {
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
        {tab === 'fuel' ? <FuelPlaceholder /> : <MaintenancePlaceholder />}
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

