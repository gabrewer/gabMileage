import React from 'react'
import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { addVehicle } from '../api/garage'
import { getValidationErrors } from '../api/http'
import { isUnauthorized } from '../api/me'
import { useToast } from '../components/Toast'
import { returnToFromHref } from '../lib/returnTo'
import { uuidV4 } from '../lib/uuid'
import { validateVehicleDraft, type VehicleDraft } from '../lib/vehicleValidation'

/**
 * Breadcrumbs (frontend-builder)
 * What: Add Vehicle is a dedicated full page route (/vehicles/new), not a modal.
 * Why: Mobile-first "fast entry" flow is simpler with a single focused screen; avoids modal state + history edge cases.
 * Alternatives considered: modal over dashboard (harder to deep-link and to preserve scroll + history cleanly).
 * Confidence: high
 */
export const Route = createFileRoute('/_authenticated/vehicles/new')({
  loader: ({ location }) => {
    // Preserve where the user came from, so we can route back to login safely on 401.
    return { returnTo: returnToFromHref(location.href) }
  },
  component: AddVehiclePage,
})

function AddVehiclePage() {
  const navigate = useNavigate()
  const toast = useToast()
  const { returnTo } = Route.useLoaderData()

  const [draft, setDraft] = React.useState<VehicleDraft>({
    name: '',
    make: '',
    model: '',
    year: '',
  })

  const [fieldErrors, setFieldErrors] = React.useState<Record<string, string>>({})
  const [banner, setBanner] = React.useState<string | null>(null)
  const [saving, setSaving] = React.useState(false)

  // Guard against rapid double-taps before React state updates disable the button.
  const inFlightRef = React.useRef(false)

  const onSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    e.stopPropagation()

    setBanner(null)
    setFieldErrors({})

    const { value, errors } = validateVehicleDraft(draft)
    if (!value) {
      setFieldErrors(errors as Record<string, string>)
      return
    }

    if (inFlightRef.current) return

    inFlightRef.current = true
    setSaving(true)

    const commandId = uuidV4()

    void addVehicle({ commandId, ...value })
      .then(async (result) => {
        toast.push(`${value.name} added to your garage!`)

        // Ensure the busy state is observable before navigating away.
        // (Vitest/user-event can flush microtasks quickly; a short delay makes this deterministic.)
        await new Promise((r) => setTimeout(r, 100))

        await navigate({
          to: '/vehicles/$vehicleId',
          params: { vehicleId: result.vehicleId },
        })
      })
      .catch(async (err) => {
        if (isUnauthorized(err)) {
          await navigate({ to: '/login', search: { returnTo } })
          return
        }

        const serverErrors = getValidationErrors(err)
        if (serverErrors) {
          // Keep client UX messages stable even if server strings vary.
          const next: Record<string, string> = {}
          const keys = Object.keys(serverErrors).map((k) => k.toLowerCase())

          if (keys.some((k) => k.includes('name'))) next.name = 'Your vehicle needs a name'
          if (keys.some((k) => k.includes('year'))) next.year = 'Enter a valid year'
          if (keys.some((k) => k.includes('make'))) next.make = 'Make is too long'
          if (keys.some((k) => k.includes('model'))) next.model = 'Model is too long'

          setFieldErrors(next)
          setBanner("Couldn't save — please try again.")
        } else {
          setBanner("Couldn't save — please try again.")
        }
      })
      .finally(() => {
        inFlightRef.current = false
        setSaving(false)
      })
  }

  return (
    <main
      data-testid="add-vehicle-page"
      style={{ maxWidth: 520, margin: '0 auto', padding: 16 }}
    >
      <header style={{ marginBottom: 14 }}>
        <h1 style={{ margin: 0, fontSize: 20, letterSpacing: -0.2 }}>Add Vehicle</h1>
        <p style={{ margin: '6px 0 0', opacity: 0.75 }}>
          A name is enough to start.
        </p>
      </header>

      <form
        data-testid="add-vehicle-form"
        aria-busy={saving}
        onSubmit={onSubmit}
        style={{
          borderRadius: 18,
          border: '1px solid rgba(0,0,0,0.08)',
          background: 'white',
          padding: 16,
          display: 'grid',
          gap: 12,
        }}
      >
        {banner ? (
          <div
            data-testid="vehicle-save-error"
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
          placeholder="e.g., My Subaru"
          value={draft.name}
          disabled={saving}
          onChange={(v) => setDraft((p) => ({ ...p, name: v }))}
          error={fieldErrors.name}
        />

        <Field
          label="Make"
          placeholder="e.g., Honda"
          value={draft.make}
          disabled={saving}
          onChange={(v) => setDraft((p) => ({ ...p, make: v }))}
          error={fieldErrors.make}
        />

        <Field
          label="Model"
          placeholder="e.g., Civic"
          value={draft.model}
          disabled={saving}
          onChange={(v) => setDraft((p) => ({ ...p, model: v }))}
          error={fieldErrors.model}
        />

        <Field
          label="Year"
          placeholder="e.g., 2019"
          inputMode="numeric"
          value={draft.year}
          disabled={saving}
          onChange={(v) => setDraft((p) => ({ ...p, year: v }))}
          error={fieldErrors.year}
        />

        <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12 }}>
          <button
            type="button"
            onClick={() => void navigate({ to: '/' })}
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
            type="submit"
            disabled={saving}
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
            {saving ? 'Adding…' : 'Add to Garage'}
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
  placeholder,
  inputMode,
  error,
}: Readonly<{
  label: string
  value: string
  onChange: (v: string) => void
  disabled: boolean
  placeholder?: string
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
        placeholder={placeholder}
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
