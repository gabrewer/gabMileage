import React from 'react'
import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { getValidationErrors } from '../api/http'
import { isUnauthorized } from '../api/me'
import { recordFuelLog, type FuelLogCalculatedField } from '../api/fuelLogs'
import { useToast } from '../components/Toast'
import { returnToFromHref } from '../lib/returnTo'
import { uuidV4 } from '../lib/uuid'
import { type FieldState, type FuelField, useFuelAutoCalc } from '../lib/fuelAutoCalc'

/**
 * TASK-005 — Log Fill-up page
 *
 * Breadcrumbs (frontend-builder)
 * What: Fill-up form uses a blur-driven auto-calc state machine (not on keystroke).
 * Why: Prevents "fighting" the user while typing, but still makes two-of-three entry fast on mobile.
 * Alternatives considered: live calc onChange (rejected: twitchy + breaks acceptance tests),
 *   forcing an explicit "Calculate" button (rejected: slower flow).
 * Confidence: high
 */

export const Route = createFileRoute('/_authenticated/vehicles/$vehicleId/fuel/log')({
  loader: ({ location }) => {
    // Preserve origin for safe redirect to login on 401.
    return { returnTo: returnToFromHref(location.href) }
  },
  component: LogFillUpRoute,
})


function LogFillUpRoute() {
  const navigate = useNavigate()
  const toast = useToast()
  const { vehicleId } = Route.useParams()
  const { returnTo } = Route.useLoaderData()

  const [date, setDate] = React.useState(() => todayLocalDate())
  const [odometer, setOdometer] = React.useState('')
  const [isPartialFillUp, setIsPartialFillUp] = React.useState(false)
  const [notes, setNotes] = React.useState('')

  const [saving, setSaving] = React.useState(false)
  const [banner, setBanner] = React.useState<string | null>(null)

  // Guard against rapid double-taps before React disables the submit button.
  const inFlightRef = React.useRef(false)

  const calc = useFuelAutoCalc()

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
          <h1 style={{ margin: 0, fontSize: 20, letterSpacing: -0.2 }}>Log Fill-up</h1>
          <p style={{ margin: '6px 0 0', opacity: 0.75 }}>
            Two numbers are enough — we’ll do the math.
          </p>
        </div>
      </header>

      <form
        aria-label="Log fill-up form"
        aria-busy={saving}
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

          setBanner(null)

          if (inFlightRef.current) return

          const toNumberOrNull = (s: string): number | null => {
            const t = s.trim()
            if (t.length === 0) return null
            const n = Number(t)
            return Number.isFinite(n) ? n : null
          }

          const toIntOrNull = (s: string): number | null => {
            const n = toNumberOrNull(s)
            if (n == null) return null
            return Math.trunc(n)
          }

          const calculatedField = ((): FuelLogCalculatedField => {
            const entries = Object.entries(calc.states) as Array<[FuelField, FieldState]>
            const found = entries.find(([, st]) => st === 'calculated')?.[0] ?? null
            return found
          })()

          const request = {
            commandId: uuidV4(),
            date: date.trim(),
            odometer: toIntOrNull(odometer) ?? 0,
            gallons: toNumberOrNull(calc.values.gallons),
            totalCost: toNumberOrNull(calc.values.totalCost),
            pricePerGallon: toNumberOrNull(calc.values.pricePerGallon),
            calculatedField,
            isPartialFillUp,
            notes: notes.trim().length === 0 ? null : notes.trim(),
          }

          inFlightRef.current = true
          setSaving(true)

          void recordFuelLog(vehicleId, request)
            .then(async () => {
              toast.push('Fill-up saved')
              await navigate({ to: '/vehicles/$vehicleId', params: { vehicleId } })
            })
            .catch(async (err) => {
              if (isUnauthorized(err)) {
                await navigate({ to: '/login', search: { returnTo } })
                return
              }

              // Keep message stable even if server strings vary.
              const validation = getValidationErrors(err)
              if (validation) {
                setBanner("Couldn't save — please check your entries.")
              } else {
                setBanner("Couldn't save — please try again.")
              }
            })
            .finally(() => {
              inFlightRef.current = false
              setSaving(false)
            })
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

        <Field
          label="Date"
          value={date}
          onChange={setDate}
          inputMode="numeric"
        />

        <Field
          label="Odometer"
          value={odometer}
          onChange={setOdometer}
          inputMode="numeric"
        />

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
          />
          Partial fill-up
        </label>

        <div style={{ display: 'grid', gap: 6 }}>
          <label
            htmlFor="field-notes"
            style={{ fontWeight: 800, fontSize: 13 }}
          >
            Notes
          </label>
          <textarea
            id="field-notes"
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            rows={3}
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
            style={{
              padding: '10px 12px',
              borderRadius: 14,
              border: 0,
              background: '#111827',
              color: 'white',
              fontWeight: 850,
              cursor: 'pointer',
              minWidth: 140,
            }}
          >
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
            // Make typing replace the calculated value (matches unit-test behavior expectations).
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
