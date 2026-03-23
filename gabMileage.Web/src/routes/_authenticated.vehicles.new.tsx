import { createFileRoute } from '@tanstack/react-router'

export const Route = createFileRoute('/_authenticated/vehicles/new')({
  component: AddVehiclePlaceholder,
})

function AddVehiclePlaceholder() {
  return (
    <main style={{ maxWidth: 640, margin: '0 auto', padding: 16 }}>
      <h1 style={{ margin: 0 }}>Add Vehicle</h1>
      <p style={{ opacity: 0.8 }}>
        This flow lands in Sprint 02. For now, your garage is ready whenever you
        are.
      </p>
      <a href="/" style={{ fontWeight: 700 }}>
        Back to Garage
      </a>
    </main>
  )
}
