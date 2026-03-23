import * as React from 'react'

// Breadcrumbs (frontend-builder)
// Who: frontend-builder
// What: Extract the fuel auto-calc state machine into a reusable hook (Log + Edit share the same logic).
// Why: Lets us unit test the state machine directly (TASK-007) and prevents logic drift between routes.
// Alternatives considered: keep duplicated hook code in each route (rejected: brittle + hard to test).
// Confidence: high

export type FuelField = 'gallons' | 'totalCost' | 'pricePerGallon'

export type FieldState = 'empty' | 'userEntered' | 'calculated'

export type FuelValues = Record<FuelField, string>

export type FuelStates = Record<FuelField, FieldState>

export const inconsistentFuelNote =
  "These numbers don't quite add up — we'll use gallons and total cost to recalculate price/gal."

export type FuelAutoCalcInitial = {
  gallons: string
  totalCost: string
  pricePerGallon: string
  calculatedField: FuelField | null
}

const defaultFuelValues: FuelValues = {
  gallons: '',
  totalCost: '',
  pricePerGallon: '',
}

const fieldIds = {
  gallons: 'field-gallons',
  totalCost: 'field-total-cost',
  pricePerGallon: 'field-price-gal',
} as const

export const fuelInputIds = new Set(Object.values(fieldIds))

export function useFuelAutoCalc(initial?: FuelAutoCalcInitial) {
  const [values, setValues] = React.useState<FuelValues>(() => ({
    gallons: initial?.gallons ?? '',
    totalCost: initial?.totalCost ?? '',
    pricePerGallon: initial?.pricePerGallon ?? '',
  }))

  const [states, setStates] = React.useState<FuelStates>(() => {
    if (!initial) {
      return {
        gallons: 'empty',
        totalCost: 'empty',
        pricePerGallon: 'empty',
      }
    }

    const seeded: FuelStates = {
      gallons: values.gallons.trim().length === 0 ? 'empty' : 'userEntered',
      totalCost: values.totalCost.trim().length === 0 ? 'empty' : 'userEntered',
      pricePerGallon: values.pricePerGallon.trim().length === 0 ? 'empty' : 'userEntered',
    }

    if (initial.calculatedField) {
      const f = initial.calculatedField
      seeded[f] = seeded[f] === 'empty' ? 'empty' : 'calculated'
    }

    return seeded
  })

  // Oldest → newest order of user-entered fields.
  const [userOrder, setUserOrder] = React.useState<FuelField[]>(() => {
    if (!initial) return []

    // If server told us which field was calculated, preserve the remaining two as the
    // initial sources. Otherwise seed all fields as "user".
    if (!initial.calculatedField) return ['gallons', 'totalCost', 'pricePerGallon']

    return (['gallons', 'totalCost', 'pricePerGallon'] as FuelField[]).filter(
      (f) => f !== initial.calculatedField,
    )
  })

  const [note, setNote] = React.useState<string | null>(null)

  const lastCalculatedFieldRef = React.useRef<FuelField | null>(initial?.calculatedField ?? null)
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
    // Only run calc when focus leaves the fuel trio.
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

    const userEntered = (Object.keys(states) as FuelField[]).filter((k) => states[k] === 'userEntered')

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
        setNote(inconsistentFuelNote)

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

export function resetFuelAutoCalcState() {
  return {
    values: { ...defaultFuelValues },
    states: {
      gallons: 'empty',
      totalCost: 'empty',
      pricePerGallon: 'empty',
    } as FuelStates,
  }
}
