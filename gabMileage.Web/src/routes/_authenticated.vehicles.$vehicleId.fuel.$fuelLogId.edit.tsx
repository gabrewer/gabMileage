import React from 'react'
import { createFileRoute, useNavigate } from '@tanstack/react-router'

import { getFuelLogs, updateFuelLog, type FuelLogCalculatedField } from '../api/fuelLogs'
import { isUnauthorized } from '../api/me'
import { useToast } from '../components/Toast'
import { type FieldState, type FuelField, useFuelAutoCalc } from '../lib/fuelAutoCalc'

/**
 * TASK-006 — Edit Fill-up page
 *
 * Breadcrumbs (frontend-builder)
 * What: Edit page reuses the same blur-driven two-of-three calc UX as Log Fill-up, but seeds its
 *   initial state from the server read model (values + calculatedField).
 * Why: Keeps edits fast on mobile and ensures the "calc" label matches what the server computed.
 * Alternatives considered: (1) disable auto-calc on edit (rejected: would drift from Log UX and
 *   lose calculatedField fidelity), (2) fetch single log by id (rejected: contract has no such endpoint).
 * Confidence: high
 */

export const Route = createFileRoute('/_authenticated/vehicles/$vehicleId/fuel/$fuelLogId/edit')({
  loader: async ({ params }) => {
    const list = await getFuelLogs(params.vehicleId)
    const log = (list.logs ?? []).find((l) => l.fuelLogId === params.fuelLogId) ?? null
    return { log }
  },
  component: EditFillUpRoute,
})


function EditFillUpRoute() {
  const navigate = useNavigate()
  const toast = useToast()
  const { vehicleId, fuelLogId } = Route.useParams()
  const { log } = Route.useLoaderData()

  const [saving, setSaving] = React.useState(false)
  const [banner, setBanner] = React.useState<string | null>(null)

  const calc = useFuelAutoCalc({
    gallons: log ? String(log.gallons) : '',
    totalCost: log ? String(log.totalCost) : '',
    pricePerGallon: log ? String(log.pricePerGallon) : '',
    calculatedField: (log?.calculatedField ?? null) as FuelField | null,
  })

  const [date, setDate] = React.useState(() => log?.date ?? todayLocalDate())
  const [odometer, setOdometer] = React.useState(() => (log ? String(log.odometer) : ''))
  const [isPartialFillUp, setIsPartialFillUp] = React.useState(() => log?.isPartialFillUp ?? false)
  const [notes, setNotes] = React.useState(() => log?.notes ?? '')

  if (!log) {
    return (
      <main style={{ maxWidth: 520, margin: '0 auto', padding: 16 }}>
        <h1 style={{ margin: 0, fontSize: 20, letterSpacing: -0.2 }}>Edit Fill-up</h1>
        <div style={{ height: 10 }} />
        <p style={{ margin: 0, opacity: 0.75 }}>This fill-up doesn’t exist.</p>
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
    setSaving(true)

    try {
      const calculatedField: FuelLogCalculatedField =
        calc.states.gallons === 'calculated'
          ? 'gallons'
          : calc.states.totalCost === 'calculated'
            ? 'totalCost'
            : calc.states.pricePerGallon === 'calculated'
              ? 'pricePerGallon'
              : null

      const toNumOrNull = (s: string): number | null => {
        const t = s.trim()
        if (t.length === 0) return null
        const n = Number(t)
        return Number.isFinite(n) ? n : null
      }

      const req = {
        date: date.trim(),
        odometer: Number.parseInt(odometer.trim(), 10),
        gallons: toNumOrNull(calc.values.gallons),
        totalCost: toNumOrNull(calc.values.totalCost),
        pricePerGallon: toNumOrNull(calc.values.pricePerGallon),
        calculatedField,
        isPartialFillUp,
        notes: notes.trim().length === 0 ? null : notes.trim(),
      }

      await updateFuelLog(vehicleId, fuelLogId, req)

      // Mark fuel logs as "dirty" so the vehicle page can refresh even though it is not mounted
      // while this edit route is active.
      try {
        window.sessionStorage.setItem(`gabMileage:fuelLogsDirty:${vehicleId}`, '1')
      } catch {
        // Ignore; refresh will still happen opportunistically via the event below.
      }

      toast.push('Changes saved')
      await navigate({ to: '/vehicles/$vehicleId', params: { vehicleId } })

      // Notify the vehicle detail page to refresh fuel logs (to get server-normalized values and MPG).
      window.dispatchEvent(
        new CustomEvent('gabMileage:fuelLogsChanged', {
          detail: { vehicleId },
        }),
      )
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
    <main style={{ maxWidth: 520, margin: '0 auto', padding: 16 }}>
      <header style={{ marginBottom: 14, display: 'flex', alignItems: 'center', gap: 12 }}>
        <button
          type="button"
          onClick={() => void navigate({ to: '/vehicles/$vehicleId', params: { vehicleId } })}
          style={{
            width: 36,
            height: 36,
            borderRadius: 12,
            border: '1px solid rgba(0,0,0,0.10)',
            display: 'grid',
            placeItems: 'center',
            background: 'white',
            fontWeight: 900,
            cursor: 'pointer',
          }}
          aria-label="Back"
        >
          ←
        </button>

        <div>
          <h1 style={{ margin: 0, fontSize: 20, letterSpacing: -0.2 }}>Edit Fill-up</h1>
          <p style={{ margin: '6px 0 0', opacity: 0.75 }}>
            Adjust anything — two numbers are still enough.
          </p>
        </div>
      </header>

      <form
        aria-label="Edit fill-up form"
        style={{
          borderRadius: 18,
          border: '1px solid rgba(0,0,0,0.08)',
          background: 'white',
          padding: 16,
          display: 'grid',
          gap: 12,
        }}
        onSubmit={(e) => {
          e.preventDefault()
          e.stopPropagation()
          void onSubmit()
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

        {calc.note ? (
          <div
            role="note"
            style={{
              borderRadius: 14,
              border: '1px solid rgba(245,158,11,0.30)',
              background: 'rgba(245,158,11,0.10)',
              padding: 10,
              fontWeight: 650,
            }}
          >
            {calc.note}
          </div>
        ) : null}

        <Field label="Date" value={date} onChange={setDate} inputMode="numeric" />
        <Field label="Odometer" value={odometer} onChange={setOdometer} inputMode="numeric" />

        <FuelFieldRow
          label="Gallons"
          value={calc.values.gallons}
          state={calc.states.gallons}
          onChange={(v) => calc.setValue('gallons', v)}
          onBlur={(e) => calc.onBlur(e)}
          inputMode="decimal"
        />

        <FuelFieldRow
          label="Total Cost"
          value={calc.values.totalCost}
          state={calc.states.totalCost}
          onChange={(v) => calc.setValue('totalCost', v)}
          onBlur={(e) => calc.onBlur(e)}
          inputMode="decimal"
        />

        <FuelFieldRow
          label="Price/gal"
          value={calc.values.pricePerGallon}
          state={calc.states.pricePerGallon}
          onChange={(v) => calc.setValue('pricePerGallon', v)}
          onBlur={(e) => calc.onBlur(e)}
          inputMode="decimal"
        />

        <label style={{ display: 'flex', alignItems: 'center', gap: 10, fontWeight: 800 }}>
          <input
            type="checkbox"
            checked={isPartialFillUp}
            onChange={(e) => setIsPartialFillUp(e.target.checked)}
            disabled={saving}
          />
          Partial fill-up
        </label>

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

        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
          <button
            type="submit"
            disabled={saving}
            style={{
              padding: '10px 12px',
              borderRadius: 14,
              border: 0,
              background: '#111827',
              color: 'white',
              fontWeight: 850,
              cursor: saving ? 'not-allowed' : 'pointer',
              minWidth: 160,
              opacity: saving ? 0.7 : 1,
            }}
          >
            {saving ? 'Saving…' : 'Save Changes'}
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
  inputMode,
}: Readonly<{
  label: string
  value: string
  onChange: (v: string) => void
  inputMode?: React.HTMLAttributes<HTMLInputElement>['inputMode']
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
        inputMode={inputMode}
        onChange={(e) => onChange(e.target.value)}
        disabled={false}
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

function FuelFieldRow({
  label,
  value,
  state,
  onChange,
  onBlur,
  inputMode,
}: Readonly<{
  label: string
  value: string
  state: FieldState
  onChange: (v: string) => void
  onBlur: (e: React.FocusEvent<HTMLInputElement>) => void
  inputMode?: React.HTMLAttributes<HTMLInputElement>['inputMode']
}>) {
  const id = `field-${label.toLowerCase().replace(/[^a-z0-9]+/g, '-')}`

  return (
    <div style={{ display: 'grid', gap: 6 }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <label htmlFor={id} style={{ fontWeight: 800, fontSize: 13 }}>
          {label}
        </label>
        {state === 'calculated' ? <CalcPill /> : null}
      </div>
      <input
        id={id}
        value={value}
        inputMode={inputMode}
        onFocus={(e) => {
          if (state === 'calculated') {
            e.currentTarget.select()
          }
        }}
        onChange={(e) => onChange(e.target.value)}
        onBlur={onBlur}
        style={{
          padding: '10px 12px',
          borderRadius: 14,
          border: `1px solid ${state === 'calculated' ? 'rgba(17,24,39,0.28)' : 'rgba(0,0,0,0.12)'}`,
          background: 'white',
        }}
      />
    </div>
  )
}

function CalcPill() {
  return (
    <span
      style={{
        padding: '3px 8px',
        borderRadius: 999,
        border: '1px solid rgba(17,24,39,0.20)',
        background: 'rgba(17,24,39,0.04)',
        fontWeight: 900,
        fontSize: 12,
        lineHeight: 1,
      }}
    >
      calc
    </span>
  )
}

function todayLocalDate() {
  const now = new Date()
  const y = now.getFullYear()
  const m = String(now.getMonth() + 1).padStart(2, '0')
  const d = String(now.getDate()).padStart(2, '0')
  return `${y}-${m}-${d}`
}
