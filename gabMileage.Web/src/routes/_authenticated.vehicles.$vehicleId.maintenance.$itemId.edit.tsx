import React from 'react'
import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { getMaintenanceTab, updateMaintenanceItem } from '../api/maintenance'
import { getValidationErrors } from '../api/http'
import { isUnauthorized } from '../api/me'
import { getVehicle } from '../api/garage'
import { useToast } from '../components/Toast'

export const Route = createFileRoute('/_authenticated/vehicles/$vehicleId/maintenance/$itemId/edit')({
  loader: async ({ params }) => {
    const vehicle = await getVehicle(params.vehicleId).catch(() => null)
    const tab = await getMaintenanceTab(params.vehicleId)
    const item = (tab.items ?? []).find((i) => i.itemId === params.itemId) ?? null
    return { vehicle, item }
  },
  component: EditMaintenanceItemRoute,
})

function EditMaintenanceItemRoute() {
  const navigate = useNavigate()
  const toast = useToast()
  const { vehicleId, itemId } = Route.useParams()
  const { vehicle, item } = Route.useLoaderData()

  const [name, setName] = React.useState(() => item?.name ?? '')
  const [mileageInterval, setMileageInterval] = React.useState(() => (item?.mileageInterval != null ? String(item.mileageInterval) : ''))
  const [timeIntervalMonths, setTimeIntervalMonths] = React.useState(() => (item?.timeIntervalMonths != null ? String(item.timeIntervalMonths) : ''))
  const [mileageWarning, setMileageWarning] = React.useState(() => String(item?.mileageWarning ?? 500))
  const [timeWarningDays, setTimeWarningDays] = React.useState(() => String(item?.timeWarningDays ?? 14))

  const [saving, setSaving] = React.useState(false)
  const [banner, setBanner] = React.useState<string | null>(null)

  const toIntOrNull = (s: string): number | null => {
    const t = s.trim()
    if (t.length === 0) return null
    const n = Number.parseInt(t, 10)
    return Number.isFinite(n) ? n : null
  }

  if (!item) {
    return (
      <main style={{ maxWidth: 520, margin: '0 auto', padding: 16 }}>
        <h1 style={{ margin: 0, fontSize: 20, letterSpacing: -0.2 }}>Edit Maintenance Item</h1>
        <div style={{ height: 10 }} />
        <p style={{ margin: 0, opacity: 0.75 }}>This maintenance item doesn’t exist.</p>
        <div style={{ height: 14 }} />
        <button
          type="button"
          onClick={() => void navigate({ to: '/vehicles/$vehicleId', params: { vehicleId } })}
          style={{
            padding: '10px 12px',
            borderRadius: 14,
            border: '1px solid rgba(0,0,0,0.12)',
            background: 'white',
            fontWeight: 800,
            cursor: 'pointer',
          }}
        >
          Back
        </button>
      </main>
    )
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

    setSaving(true)
    try {
      await updateMaintenanceItem(vehicleId, itemId, {
        name: trimmedName,
        mileageInterval: miles,
        timeIntervalMonths: months,
        mileageWarning: milesWarn,
        timeWarningDays: timeWarn,
      })

      toast.push('Changes saved')
      await navigate({ to: '/vehicles/$vehicleId', params: { vehicleId } })
    } catch (err) {
      if (isUnauthorized(err)) {
        window.location.assign(`/login?returnTo=${encodeURIComponent(window.location.pathname)}`)
        return
      }

      const validation = getValidationErrors(err)
      if (validation) {
        setBanner("Couldn't save — please check your entries.")
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
          <h1 style={{ margin: 0, fontSize: 20, letterSpacing: -0.2 }}>Edit Maintenance Item</h1>
          {vehicle ? <p style={{ margin: '6px 0 0', opacity: 0.75 }}>for {vehicle.name}</p> : null}
        </div>
      </header>

      <form
        aria-label="Edit maintenance item form"
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

        <Field label="Name" value={name} onChange={setName} disabled={saving} />

        <Field label="Mileage Interval" value={mileageInterval} onChange={setMileageInterval} disabled={saving} inputMode="numeric" />
        <Field label="Time Interval (months)" value={timeIntervalMonths} onChange={setTimeIntervalMonths} disabled={saving} inputMode="numeric" />

        <section style={{ borderTop: '1px solid rgba(0,0,0,0.06)', paddingTop: 10 }}>
          <div style={{ fontWeight: 900, letterSpacing: -0.2, marginBottom: 10 }}>When to warn me</div>
          <Field label="Warn me (miles before due)" value={mileageWarning} onChange={setMileageWarning} disabled={saving} inputMode="numeric" />
          <div style={{ height: 10 }} />
          <Field label="Warn me (days before due)" value={timeWarningDays} onChange={setTimeWarningDays} disabled={saving} inputMode="numeric" />
        </section>

        <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
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
}: Readonly<{
  label: string
  value: string
  onChange: (v: string) => void
  disabled: boolean
  inputMode?: React.HTMLAttributes<HTMLInputElement>['inputMode']
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
        onChange={(e) => onChange(e.target.value)}
        style={{
          padding: '10px 12px',
          borderRadius: 14,
          border: '1px solid rgba(0,0,0,0.12)',
          background: 'white',
        }}
      />
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
