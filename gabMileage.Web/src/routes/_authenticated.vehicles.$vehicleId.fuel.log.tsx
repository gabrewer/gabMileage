import React from 'react'
import { createFileRoute, useNavigate } from '@tanstack/react-router'

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
  component: LogFillUpRoute,
})

type FuelField = 'gallons' | 'totalCost' | 'pricePerGallon'

type FieldState = 'empty' | 'userEntered' | 'calculated'

type FuelValues = Record<FuelField, string>

type FuelStates = Record<FuelField, FieldState>

const inconsistentNote =
  "These numbers don't quite add up — we'll use gallons and total cost to recalculate price/gal."

function LogFillUpRoute() {
  const navigate = useNavigate()
  const { vehicleId } = Route.useParams()

  const [date, setDate] = React.useState(() => todayLocalDate())
  const [odometer, setOdometer] = React.useState('')
  const [isPartialFillUp, setIsPartialFillUp] = React.useState(false)
  const [notes, setNotes] = React.useState('')

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
          // Submit wiring (POST) is out of scope for the unit tests in TASK-005.
        }}
      >
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

function useFuelAutoCalc() {
  const fuelInputIds = React.useMemo(
    () => new Set(['field-gallons', 'field-total-cost', 'field-price-gal']),
    [],
  )

  const [values, setValues] = React.useState<FuelValues>({
    gallons: '',
    totalCost: '',
    pricePerGallon: '',
  })

  const [states, setStates] = React.useState<FuelStates>({
    gallons: 'empty',
    totalCost: 'empty',
    pricePerGallon: 'empty',
  })

  // Oldest → newest order of user-entered fields.
  const [userOrder, setUserOrder] = React.useState<FuelField[]>([])

  const [note, setNote] = React.useState<string | null>(null)

  // Track last calculated field so that if the user edits it, we can reassign which field is
  // calculated using LIFO (last two user-entered fields become sources).
  const lastCalculatedFieldRef = React.useRef<FuelField | null>(null)
  const reassignPendingRef = React.useRef(false)

  const setValue = (field: FuelField, raw: string) => {
    setNote(null)

    const trimmed = raw.trim()

    setValues((prev) => ({ ...prev, [field]: raw }))

    setStates((prev) => {
      const nextState: FieldState = trimmed.length === 0 ? 'empty' : 'userEntered'

      const isEditingLastCalculated =
        prev[field] === 'calculated' && lastCalculatedFieldRef.current === field

      // If the user touches the previously-calculated field (including clearing it), we enter
      // "reassign" mode so their next blur reassigns which field is calculated (LIFO).
      if (isEditingLastCalculated) {
        reassignPendingRef.current = true
      } else if (nextState === 'empty') {
        // Clearing a normal user-entered field resets reassignment.
        reassignPendingRef.current = false
      }

      return { ...prev, [field]: nextState }
    })

    setUserOrder((prev) => {
      const without = prev.filter((f) => f !== field)
      if (trimmed.length === 0) return without
      return [...without, field]
    })
  }

  const onBlur = (e?: React.FocusEvent<HTMLInputElement>) => {
    // Breadcrumbs (frontend-builder)
    // What: Only run the calc pass when focus leaves the fuel trio (e.g., to Odometer), not when
    //   tabbing/clicking between the three fuel inputs.
    // Why: Keeps the UX predictable and matches Vitest expectations that the final blur triggers
    //   evaluation (including inconsistency handling).
    // Alternatives considered: calc on every intra-field blur (rejected: races user typing + breaks tests).
    // Confidence: high
    const next = e?.relatedTarget
    if (next instanceof HTMLElement && fuelInputIds.has(next.id)) {
      return
    }

    setNote(null)

    const num = (s: string): number | null => {
      const t = s.trim()
      if (t.length === 0) return null
      const n = Number(t)
      return Number.isFinite(n) ? n : null
    }

    const gallons = num(values.gallons)
    const totalCost = num(values.totalCost)
    const price = num(values.pricePerGallon)

    const userEntered = (Object.keys(states) as FuelField[]).filter(
      (k) => states[k] === 'userEntered',
    )

    const setCalculated = (field: FuelField) => {
      lastCalculatedFieldRef.current = field
      reassignPendingRef.current = false
    }

    // LIFO reassignment path: user edited the previously-calculated field.
    if (userEntered.length === 3 && reassignPendingRef.current) {
      const sources = userOrder.slice(-2)
      if (sources.length !== 2) return

      const target = (['gallons', 'totalCost', 'pricePerGallon'] as const).find(
        (f) => f !== sources[0] && f !== sources[1],
      )
      if (!target) return

      // Compute target from sources.
      if (target === 'pricePerGallon') {
        if (gallons == null || totalCost == null || gallons <= 0) return
        const computed = round(totalCost / gallons, 3)
        setValues((prev) => ({ ...prev, pricePerGallon: formatForInput(computed, 3) }))
        setStates({
          gallons: sources.includes('gallons') ? 'userEntered' : 'calculated',
          totalCost: sources.includes('totalCost') ? 'userEntered' : 'calculated',
          pricePerGallon: sources.includes('pricePerGallon') ? 'userEntered' : 'calculated',
        })
        setUserOrder(sources)
        setCalculated('pricePerGallon')
        return
      }

      if (target === 'totalCost') {
        if (gallons == null || price == null) return
        const computed = round(gallons * price, 2)
        setValues((prev) => ({ ...prev, totalCost: formatForInput(computed, 2) }))
        setStates({
          gallons: sources.includes('gallons') ? 'userEntered' : 'calculated',
          totalCost: sources.includes('totalCost') ? 'userEntered' : 'calculated',
          pricePerGallon: sources.includes('pricePerGallon') ? 'userEntered' : 'calculated',
        })
        setUserOrder(sources)
        setCalculated('totalCost')
        return
      }

      if (target === 'gallons') {
        if (totalCost == null || price == null || price <= 0) return
        const computed = round(totalCost / price, 3)
        setValues((prev) => ({ ...prev, gallons: formatForInput(computed, 3) }))
        setStates({
          gallons: sources.includes('gallons') ? 'userEntered' : 'calculated',
          totalCost: sources.includes('totalCost') ? 'userEntered' : 'calculated',
          pricePerGallon: sources.includes('pricePerGallon') ? 'userEntered' : 'calculated',
        })
        setUserOrder(sources)
        setCalculated('gallons')
      }

      return
    }

    // All-three path: if inconsistent, prioritize gallons + totalCost → recalc price/gal.
    if (userEntered.length === 3 && gallons != null && totalCost != null && price != null) {
      // Guard divide-by-zero / non-sensical gallons.
      if (gallons <= 0) {
        return
      }

      const expected = totalCost / gallons
      if (!Number.isFinite(expected)) return

      const expectedNorm = round(expected, 3)
      const providedNorm = round(price, 3)

      if (Math.abs(expectedNorm - providedNorm) > 0.01) {
        setNote(inconsistentNote)

        setValues((prev) => ({
          ...prev,
          pricePerGallon: formatForInput(expectedNorm, 3),
        }))

        setStates({
          gallons: 'userEntered',
          totalCost: 'userEntered',
          pricePerGallon: 'calculated',
        })

        setUserOrder(['gallons', 'totalCost'])
        setCalculated('pricePerGallon')
        return
      }

      // Consistent: no calculated field.
      setStates({
        gallons: 'userEntered',
        totalCost: 'userEntered',
        pricePerGallon: 'userEntered',
      })
      return
    }

    // Exactly two user-entered fields → calculate the third on blur.
    if (userEntered.length !== 2) return

    const missing = (['gallons', 'totalCost', 'pricePerGallon'] as FuelField[]).find(
      (f) => states[f] !== 'userEntered',
    )
    if (!missing) return

    // Require the two sources to be numeric.
    if (missing === 'pricePerGallon') {
      if (gallons == null || totalCost == null || gallons <= 0) return
      const computed = round(totalCost / gallons, 3)
      setValues((prev) => ({ ...prev, pricePerGallon: formatForInput(computed, 3) }))
      setStates((prev) => ({ ...prev, pricePerGallon: 'calculated' }))
      setCalculated('pricePerGallon')
      return
    }

    if (missing === 'totalCost') {
      if (gallons == null || price == null) return
      const computed = round(gallons * price, 2)
      setValues((prev) => ({ ...prev, totalCost: formatForInput(computed, 2) }))
      setStates((prev) => ({ ...prev, totalCost: 'calculated' }))
      setCalculated('totalCost')
      return
    }

    if (missing === 'gallons') {
      if (totalCost == null || price == null || price <= 0) return
      const computed = round(totalCost / price, 3)
      setValues((prev) => ({ ...prev, gallons: formatForInput(computed, 3) }))
      setStates((prev) => ({ ...prev, gallons: 'calculated' }))
      setCalculated('gallons')
    }
  }

  return { values, states, setValue, onBlur, note, userOrder }
}

function round(value: number, dp: number) {
  const factor = 10 ** dp
  return Math.round(value * factor) / factor
}

function formatForInput(value: number, dp: number) {
  const fixed = value.toFixed(dp)
  return fixed.replace(/\.?0+$/, '')
}

function todayLocalDate() {
  const now = new Date()
  const y = now.getFullYear()
  const m = String(now.getMonth() + 1).padStart(2, '0')
  const d = String(now.getDate()).padStart(2, '0')
  return `${y}-${m}-${d}`
}
