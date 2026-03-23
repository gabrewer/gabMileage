export type VehicleDraft = {
  name: string
  make: string
  model: string
  year: string
}

export type VehicleNormalized = {
  name: string
  make: string | null
  model: string | null
  year: number | null
}

export type VehicleFieldErrors = Partial<Record<keyof VehicleDraft, string>> & {
  form?: string
}

function nullIfBlank(value: string): string | null {
  const trimmed = value.trim()
  return trimmed.length === 0 ? null : trimmed
}

/**
 * Breadcrumbs (frontend-builder)
 * What: Shared client-side validation/normalization for Add + Edit vehicle.
 * Why: Keeps UX messages stable and aligned with Sprint 02 brief; avoids duplicating trim/range logic.
 * Alternatives considered: server-only validation (slower, worse UX), validating on blur (rejected by brief).
 * Confidence: high
 */
export function validateVehicleDraft(
  draft: VehicleDraft,
): { value: VehicleNormalized | null; errors: VehicleFieldErrors } {
  const errors: VehicleFieldErrors = {}

  const name = draft.name.trim()
  const make = nullIfBlank(draft.make)
  const model = nullIfBlank(draft.model)

  if (name.length === 0) {
    errors.name = 'Your vehicle needs a name'
  } else if (name.length > 50) {
    errors.name = 'Name is too long'
  }

  if (make && make.length > 50) {
    errors.make = 'Make is too long'
  }

  if (model && model.length > 50) {
    errors.model = 'Model is too long'
  }

  const rawYear = draft.year.trim()
  let year: number | null = null
  if (rawYear.length > 0) {
    if (!/^\d{4}$/.test(rawYear)) {
      errors.year = 'Enter a valid year'
    } else {
      const parsed = Number.parseInt(rawYear, 10)
      const max = new Date().getFullYear() + 1
      if (!Number.isFinite(parsed) || parsed < 1900 || parsed > max) {
        errors.year = 'Enter a valid year'
      } else {
        year = parsed
      }
    }
  }

  const hasErrors = Object.keys(errors).length > 0
  if (hasErrors) return { value: null, errors }

  return {
    value: {
      name,
      make,
      model,
      year,
    },
    errors: {},
  }
}
