import React from 'react'
import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { defineMaintenanceItem } from '../api/maintenance'
import { getValidationErrors } from '../api/http'
import { isUnauthorized } from '../api/me'
import { getVehicle } from '../api/garage'
import { useToast } from '../components/Toast'
import { uuidV4 } from '../lib/uuid'

export const Route = createFileRoute('/_authenticated/vehicles/$vehicleId/maintenance/new')({
  loader: async ({ params }) => {
    // Optional context (safe for tests; they already stub GET vehicle).
    const vehicle = await getVehicle(params.vehicleId).catch(() => null)
    return { vehicle }
  },
  component: AddMaintenanceItemRoute,
})

/**
 * TASK-005 — Add Maintenance Item page
 *
 * Breadcrumbs (frontend-builder)
 * What: Warning thresholds are collapsed by default with sensible defaults (500 mi / 14 days).
 * Why: Keeps the happy path simple while still letting advanced users tune "due soon" behavior.
 * Alternatives considered: always-visible thresholds (rejected: form feels intimidating), hiding thresholds entirely (rejected: power-users).
 * Confidence: high
 */
function AddMaintenanceItemRoute() {
  const navigate = useNavigate()
  const toast = useToast()
  const { vehicleId } = Route.useParams()
  const { vehicle } = Route.useLoaderData()

  const [name, setName] = React.useState('')
  const [mileageInterval, setMileageInterval] = React.useState('')
  const [timeIntervalMonths, setTimeIntervalMonths] = React.useState('')

  const [warnOpen, setWarnOpen] = React.useState(false)
  const [mileageWarning, setMileageWarning] = React.useState('500')
  const [timeWarningDays, setTimeWarningDays] = React.useState('14')

  const [initialLastPerformedDate, setInitialLastPerformedDate] = React.useState('')
  const [initialLastPerformedOdometer, setInitialLastPerformedOdometer] = React.useState('')

  const [saving, setSaving] = React.useState(false)
  const [banner, setBanner] = React.useState<string | null>(null)

  const toIntOrNull = (s: string): number | null => {
    const t = s.trim()
    if (t.length === 0) return null
    const n = Number.parseInt(t, 10)
    return Number.isFinite(n) ? n : null
  }

  const onSubmit = async () => {
    setBanner(null)

    const trimmedName = name.trim()
    const miles = toIntOrNull(mileageInterval)
    const months = toIntOrNull(timeIntervalMonths)

    const normalizeOrDefault = (n: number | null, def: number) => (n == null || n <= 0 ? def : n)

    if (trimmedName.length === 0) {
      setBanner('Give this item a name')
      return
    }

    if (miles != null && (miles <= 0 || miles > 999_999)) {
      setBanner('Enter a valid mileage interval')
      return
    }

    if (months != null && (months <= 0 || months > 120)) {
      setBanner('Enter a valid time interval (1–120 months)')
      return
    }

    if (miles == null && months == null) {
      // Keep copy aligned with API validation message; tests only assert /interval/i.
      setBanner('Set a mileage interval, time interval, or both')
      return
    }

    const milesWarn = normalizeOrDefault(toIntOrNull(mileageWarning), 500)
    const timeWarn = normalizeOrDefault(toIntOrNull(timeWarningDays), 14)

    if (miles != null && milesWarn >= miles) {
      setBanner('Warning threshold must be less than the interval')
      return
    }

    if (months != null) {
      const intervalDays = months * 30.44
      if (timeWarn >= intervalDays) {
        setBanner('Warning threshold must be less than the interval')
        return
      }
    }

    const initialOdo = toIntOrNull(initialLastPerformedOdometer)
    if (initialOdo != null && (initialOdo <= 0 || initialOdo > 9_999_999)) {
      setBanner('Enter a valid odometer reading')
      return
    }

    setSaving(true)
    try {
      const req = {
        commandId: uuidV4(),
        name: trimmedName,
        mileageInterval: miles,
        timeIntervalMonths: months,
        ...(miles != null ? { mileageWarning: milesWarn } : {}),
        ...(months != null ? { timeWarningDays: timeWarn } : {}),
        initialLastPerformedDate: initialLastPerformedDate.trim().length === 0 ? null : initialLastPerformedDate.trim(),
        initialLastPerformedOdometer: initialOdo,
      }

      await defineMaintenanceItem(vehicleId, req)
      toast.push(`${trimmedName} added`)
      await navigate({ to: '/vehicles/$vehicleId', params: { vehicleId } })
    } catch (err) {
      if (isUnauthorized(err)) {
        window.location.assign(`/login?returnTo=${encodeURIComponent(window.location.pathname)}`)
        return
      }

      const validation = getValidationErrors(err)
      if (validation) {
        // Prefer the server-provided interval validation message if present.
        const interval = validation.intervals?.[0]
        setBanner(interval ?? "Couldn't save — please check your entries.")
      } else {
        setBanner("Couldn't save — please try again.")
      }
    } finally {
      setSaving(false)
    }
  }

  return (
    <main style={{ maxWidth: 520, margin: '0 auto', padding: 16 }}>
      <header style={{ marginBottom: 14, display: 'flex', alignItems: 'center', gap: 12 }}>
        <button
          type="button"
          onClick={() => void navigate({ to: '/vehicles/$vehicleId', params: { vehicleId } })}
          style={backButtonStyle}
          aria-label="Back"
        >
          ←
        </button>
        <div>
          <h1 style={{ margin: 0, fontSize: 20, letterSpacing: -0.2 }}>Add Maintenance Item</h1>
          {vehicle ? (
            <p style={{ margin: '6px 0 0', opacity: 0.75 }}>for {vehicle.name}</p>
          ) : null}
        </div>
      </header>

      <form
        aria-label="Add maintenance item form"
        aria-busy={saving}
        style={cardStyle}
        onSubmit={(e) => {
          e.preventDefault()
          e.stopPropagation()
          void onSubmit()
        }}
      >
        {banner ? (
          <div role="alert" style={alertStyle}>
            {banner}
          </div>
        ) : null}

        <Field label="Name" value={name} onChange={setName} disabled={saving} placeholder="e.g., Oil Change" />

        <Field
          label="Mileage Interval"
          value={mileageInterval}
          onChange={setMileageInterval}
          disabled={saving}
          inputMode="numeric"
          placeholder="Every ___ miles"
          help="Leave blank if this item is time-based only."
        />

        <div style={{ opacity: 0.7, fontSize: 13, marginTop: -6 }}>
          If you set both, the item will be due at whichever comes first.
        </div>

        <Field
          label="Time Interval (months)"
          value={timeIntervalMonths}
          onChange={setTimeIntervalMonths}
          disabled={saving}
          inputMode="numeric"
          placeholder="Every ___ months"
          help="Leave blank if this item is mileage-based only."
        />

        <section style={{ borderTop: '1px solid rgba(0,0,0,0.06)', paddingTop: 10 }}>
          <button
            type="button"
            onClick={() => setWarnOpen((p) => !p)}
            disabled={saving}
            style={{
              border: 0,
              background: 'transparent',
              padding: 0,
              fontWeight: 900,
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'baseline',
              gap: 10,
            }}
          >
            <span>When to warn me</span>
            <span style={{ opacity: 0.7, fontWeight: 800, fontSize: 12 }}>
              Defaults: 500 mi / 14 days
            </span>
            <span style={{ marginLeft: 'auto', opacity: 0.6 }}>{warnOpen ? '▾' : '▸'}</span>
          </button>

          {warnOpen ? (
            <div style={{ marginTop: 10, display: 'grid', gap: 12 }}>
              <Field
                label="Warn me (miles before due)"
                value={mileageWarning}
                onChange={setMileageWarning}
                disabled={saving}
                inputMode="numeric"
              />
              <Field
                label="Warn me (days before due)"
                value={timeWarningDays}
                onChange={setTimeWarningDays}
                disabled={saving}
                inputMode="numeric"
              />
              <div style={{ opacity: 0.75, fontSize: 13 }}>
                We'll mark the item as "due soon" when it's within these thresholds.
              </div>
            </div>
          ) : null}
        </section>

        <section style={{ borderTop: '1px solid rgba(0,0,0,0.06)', paddingTop: 10 }}>
          <div style={{ fontWeight: 900, letterSpacing: -0.2, marginBottom: 4 }}>Starting point</div>
          <div style={{ opacity: 0.75, fontSize: 13, marginBottom: 10 }}>
            If you know when this was last done, enter it so we can track from the right baseline.
          </div>

          <Field
            label="When was this last done?"
            value={initialLastPerformedDate}
            onChange={setInitialLastPerformedDate}
            disabled={saving}
            inputMode="numeric"
            placeholder="YYYY-MM-DD"
          />

          <Field
            label="Odometer at last service"
            value={initialLastPerformedOdometer}
            onChange={setInitialLastPerformedOdometer}
            disabled={saving}
            inputMode="numeric"
            placeholder="e.g., 45230"
          />
        </section>

        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
          <button type="submit" disabled={saving} style={primaryButtonStyle(saving)}>
            Save
          </button>
        </div>
      </form>
    </main>
  )
}

function Field({
  label,
  value,
  onChange,
  disabled,
  inputMode,
  placeholder,
  help,
}: Readonly<{
  label: string
  value: string
  onChange: (v: string) => void
  disabled: boolean
  inputMode?: React.HTMLAttributes<HTMLInputElement>['inputMode']
  placeholder?: string
  help?: string
}>) {
  const id = `field-${label.toLowerCase().replace(/[^a-z0-9]+/g, '-')}`

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
        placeholder={placeholder}
        onChange={(e) => onChange(e.target.value)}
        style={{
          padding: '10px 12px',
          borderRadius: 14,
          border: '1px solid rgba(0,0,0,0.12)',
          background: 'white',
        }}
      />
      {help ? <div style={{ opacity: 0.7, fontSize: 12 }}>{help}</div> : null}
    </div>
  )
}

const backButtonStyle: React.CSSProperties = {
  width: 36,
  height: 36,
  borderRadius: 12,
  border: '1px solid rgba(0,0,0,0.10)',
  display: 'grid',
  placeItems: 'center',
  background: 'white',
  fontWeight: 900,
  cursor: 'pointer',
}

const cardStyle: React.CSSProperties = {
  borderRadius: 18,
  border: '1px solid rgba(0,0,0,0.08)',
  background: 'white',
  padding: 16,
  display: 'grid',
  gap: 12,
}

const alertStyle: React.CSSProperties = {
  borderRadius: 14,
  border: '1px solid rgba(244,63,94,0.30)',
  background: 'rgba(244,63,94,0.08)',
  padding: 10,
  fontWeight: 650,
}

function primaryButtonStyle(disabled: boolean): React.CSSProperties {
  return {
    padding: '10px 12px',
    borderRadius: 12,
    border: 0,
    background: '#111827',
    color: 'white',
    fontWeight: 900,
    cursor: disabled ? 'not-allowed' : 'pointer',
    opacity: disabled ? 0.75 : 1,
    minWidth: 120,
  }
}
