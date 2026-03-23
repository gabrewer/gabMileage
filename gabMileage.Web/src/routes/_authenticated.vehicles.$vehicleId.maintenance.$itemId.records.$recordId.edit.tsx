import React from 'react'
import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { getMaintenanceTab, updateMaintenanceRecord, deleteMaintenanceRecord } from '../api/maintenance'
import { getValidationErrors } from '../api/http'
import { isUnauthorized } from '../api/me'
import { getVehicle } from '../api/garage'
import { useToast } from '../components/Toast'

export const Route = createFileRoute(
  '/_authenticated/vehicles/$vehicleId/maintenance/$itemId/records/$recordId/edit',
)({
  loader: async ({ params }) => {
    const vehicle = await getVehicle(params.vehicleId).catch(() => null)
    const tab = await getMaintenanceTab(params.vehicleId)
    const item = (tab.items ?? []).find((i) => i.itemId === params.itemId) ?? null
    const record = item?.records?.find((r) => r.recordId === params.recordId) ?? null
    return { vehicle, item, record }
  },
  component: EditServiceRecordRoute,
})

function formatLocalDate(value: string): string {
  const [y, m, d] = value.split('-').map((p) => Number.parseInt(p, 10))
  const date = new Date(y, m - 1, d)
  return new Intl.DateTimeFormat('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  }).format(date)
}

function EditServiceRecordRoute() {
  const navigate = useNavigate()
  const toast = useToast()
  const { vehicleId, itemId, recordId } = Route.useParams()
  const { vehicle, item, record } = Route.useLoaderData()

  const [date, setDate] = React.useState(() => record?.date ?? '')
  const [odometer, setOdometer] = React.useState(() => (record ? String(record.odometer) : ''))
  const [cost, setCost] = React.useState(() => (record?.cost != null ? String(record.cost) : ''))
  const [notes, setNotes] = React.useState(() => record?.notes ?? '')

  const [saving, setSaving] = React.useState(false)
  const [banner, setBanner] = React.useState<string | null>(null)

  const [confirmDelete, setConfirmDelete] = React.useState(false)
  const [deleting, setDeleting] = React.useState(false)

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

  if (!item || !record) {
    return (
      <main style={{ maxWidth: 520, margin: '0 auto', padding: 16 }}>
        <h1 style={{ margin: 0, fontSize: 20, letterSpacing: -0.2 }}>Edit Service Record</h1>
        <div style={{ height: 10 }} />
        <p style={{ margin: 0, opacity: 0.75 }}>This service record doesn’t exist.</p>
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
      await updateMaintenanceRecord(vehicleId, itemId, recordId, {
        date: date.trim(),
        odometer: odo,
        cost: toNumOrNull(cost),
        notes: notes.trim().length === 0 ? null : notes.trim(),
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

  const onDelete = async () => {
    setDeleting(true)
    try {
      await deleteMaintenanceRecord(vehicleId, itemId, recordId)
      toast.push('Service record deleted')
      await navigate({ to: '/vehicles/$vehicleId', params: { vehicleId } })
    } catch {
      toast.push("Couldn't delete service record — please try again.")
    } finally {
      setDeleting(false)
      setConfirmDelete(false)
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
          <h1 style={{ margin: 0, fontSize: 20, letterSpacing: -0.2 }}>Edit Service Record</h1>
          <p style={{ margin: '6px 0 0', opacity: 0.75 }}>
            {item.name}
            {vehicle ? ` for ${vehicle.name}` : ''}
          </p>
        </div>
      </header>

      <form
        aria-label="Edit service record form"
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
        <Field label="Odometer" value={odometer} onChange={setOdometer} disabled={saving} inputMode="numeric" />
        <Field label="Cost" value={cost} onChange={setCost} disabled={saving} inputMode="decimal" />

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
            style={{
              padding: '10px 12px',
              borderRadius: 14,
              border: '1px solid rgba(0,0,0,0.12)',
              background: 'white',
              resize: 'vertical',
            }}
          />
        </div>

        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 10 }}>
          <button
            type="button"
            onClick={() => setConfirmDelete(true)}
            disabled={saving}
            style={{
              border: 0,
              background: 'transparent',
              padding: 0,
              color: '#be123c',
              fontWeight: 850,
              cursor: 'pointer',
            }}
          >
            Delete this record
          </button>

          <button type="submit" disabled={saving} style={primaryButtonStyle(saving)}>
            Save
          </button>
        </div>
      </form>

      {confirmDelete ? (
        <ConfirmDialog
          title={`Delete this service record from ${formatLocalDate(record.date)}?`}
          description="This can’t be undone."
          disabled={deleting}
          onCancel={() => setConfirmDelete(false)}
          onConfirm={onDelete}
        />
      ) : null}
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

function ConfirmDialog({
  title,
  description,
  disabled,
  onCancel,
  onConfirm,
}: Readonly<{
  title: string
  description: string
  disabled: boolean
  onCancel: () => void
  onConfirm: () => void
}>) {
  return (
    <div
      role="dialog"
      aria-modal="true"
      style={{
        position: 'fixed',
        inset: 0,
        background: 'rgba(17,24,39,0.35)',
        display: 'grid',
        placeItems: 'center',
        padding: 16,
        zIndex: 60,
      }}
    >
      <div
        style={{
          width: 'min(520px, 100%)',
          borderRadius: 18,
          border: '1px solid rgba(0,0,0,0.10)',
          background: 'white',
          padding: 16,
          boxShadow: '0 20px 60px rgba(0,0,0,0.20)',
          display: 'grid',
          gap: 12,
        }}
      >
        <div style={{ fontWeight: 900, letterSpacing: -0.2 }}>{title}</div>
        <div style={{ opacity: 0.78 }}>{description}</div>

        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
          <button
            type="button"
            onClick={onCancel}
            disabled={disabled}
            style={{
              border: 0,
              background: 'transparent',
              padding: '10px 12px',
              borderRadius: 12,
              fontWeight: 850,
              cursor: disabled ? 'not-allowed' : 'pointer',
              opacity: disabled ? 0.7 : 1,
            }}
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={onConfirm}
            disabled={disabled}
            style={{
              padding: '10px 12px',
              borderRadius: 12,
              border: 0,
              background: '#be123c',
              color: 'white',
              fontWeight: 900,
              cursor: disabled ? 'not-allowed' : 'pointer',
              opacity: disabled ? 0.7 : 1,
              minWidth: 120,
            }}
          >
            Delete
          </button>
        </div>
      </div>
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
