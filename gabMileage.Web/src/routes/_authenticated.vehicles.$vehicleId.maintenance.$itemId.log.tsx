import React from 'react'
import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { recordMaintenance, getMaintenanceTab } from '../api/maintenance'
import { getValidationErrors } from '../api/http'
import { isUnauthorized } from '../api/me'
import { getVehicle } from '../api/garage'
import { useToast } from '../components/Toast'
import { uuidV4 } from '../lib/uuid'

export const Route = createFileRoute('/_authenticated/vehicles/$vehicleId/maintenance/$itemId/log')({
  loader: async ({ params }) => {
    const vehicle = await getVehicle(params.vehicleId).catch(() => null)
    const tab = await getMaintenanceTab(params.vehicleId)
    const item = (tab.items ?? []).find((i) => i.itemId === params.itemId) ?? null
    return { vehicle, item, lastOdometer: tab.lastOdometer }
  },
  component: LogServiceRoute,
})

function todayLocalDate(): string {
  const d = new Date()
  const y = d.getFullYear()
  const m = `${d.getMonth() + 1}`.padStart(2, '0')
  const day = `${d.getDate()}`.padStart(2, '0')
  return `${y}-${m}-${day}`
}

function LogServiceRoute() {
  const navigate = useNavigate()
  const toast = useToast()
  const { vehicleId, itemId } = Route.useParams()
  const { vehicle, item, lastOdometer } = Route.useLoaderData()

  const [date, setDate] = React.useState(() => todayLocalDate())
  const [odometer, setOdometer] = React.useState('')
  const [cost, setCost] = React.useState('')
  const [notes, setNotes] = React.useState('')

  const [saving, setSaving] = React.useState(false)
  const [banner, setBanner] = React.useState<string | null>(null)

  const toInt = (s: string): number | null => {
    const t = s.trim()
    if (t.length === 0) return null
    const n = Number.parseInt(t, 10)
    return Number.isFinite(n) ? n : null
  }

  const toNumOrNull = (s: string): number | null => {
    const t = s.trim()
    if (t.length === 0) return null
    const n = Number(t)
    return Number.isFinite(n) ? n : null
  }

  if (!item) {
    return (
      <main style={{ maxWidth: 520, margin: '0 auto', padding: 16 }}>
        <h1 style={{ margin: 0, fontSize: 20, letterSpacing: -0.2 }}>Log Service</h1>
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

    const odo = toInt(odometer)
    if (odo == null || odo <= 0 || odo > 9_999_999) {
      setBanner('Enter a valid odometer reading')
      return
    }

    setSaving(true)
    try {
      const req = {
        commandId: uuidV4(),
        date: date.trim(),
        odometer: odo,
        cost: toNumOrNull(cost),
        notes: notes.trim().length === 0 ? null : notes.trim(),
      }

      await recordMaintenance(vehicleId, itemId, req)
      toast.push('Service recorded!')
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
          <h1 style={{ margin: 0, fontSize: 20, letterSpacing: -0.2 }}>Log Service</h1>
          <p style={{ margin: '6px 0 0', opacity: 0.75 }}>
            {item.name}
            {vehicle ? ` for ${vehicle.name}` : ''}
          </p>
        </div>
      </header>

      <form
        aria-label="Log service form"
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

        <Field label="Date" value={date} onChange={setDate} disabled={saving} inputMode="numeric" />

        <Field
          label="Odometer"
          value={odometer}
          onChange={setOdometer}
          disabled={saving}
          inputMode="numeric"
          help={lastOdometer != null ? `Last reading: ${lastOdometer.toLocaleString()} mi` : undefined}
        />

        <Field
          label="Cost"
          value={cost}
          onChange={setCost}
          disabled={saving}
          inputMode="decimal"
          placeholder="Cost (optional)"
        />

        <div style={{ display: 'grid', gap: 6 }}>
          <label htmlFor="field-notes" style={{ fontWeight: 800, fontSize: 13 }}>
            Notes
          </label>
          <textarea
            id="field-notes"
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            rows={3}
            disabled={saving}
            placeholder="Any notes? (optional)"
            style={{
              padding: '10px 12px',
              borderRadius: 14,
              border: '1px solid rgba(0,0,0,0.12)',
              background: 'white',
              resize: 'vertical',
            }}
          />
        </div>

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
