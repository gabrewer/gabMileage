# Sprint 02 — Garage & Vehicles

> **Milestone 2 of 6** | Depends on: Sprint 01 (Foundation & Auth)
> **Goal**: Users can add vehicles to their garage, edit vehicle profiles, and see them on the dashboard as a card grid. Vehicle detail view shows the profile header (tabs come in later sprints).

---

## User Stories

### US-2.1: User adds a vehicle
**As a** user, **I want to** add a vehicle to my garage with just a name **so that** I can start tracking it immediately without friction.

### US-2.2: User sees their garage
**As a** user with one or more vehicles, **I want to** see all my vehicles as cards on the dashboard **so that** I can quickly access any of them.

### US-2.3: User views vehicle details
**As a** user, **I want to** tap on a vehicle card to see its detail page **so that** I can view and manage that vehicle.

### US-2.4: User edits a vehicle profile
**As a** user, **I want to** edit my vehicle's name, make, model, and year **so that** I can keep the profile accurate as things change.

---

## Screen Descriptions

### Dashboard — With Vehicles (Garage Card Grid)

**What the user sees:**
- Top bar (from Sprint 01)
- **No "Needs Attention" strip yet** — that's Sprint 05
- **Garage section heading**: "Your Garage" with a "+ Add Vehicle" button to the right
- **Card grid**: One card per vehicle
  - Each card shows:
    - Vehicle name (e.g., "Blue Thunder") — prominently displayed
    - Subtitle: make/model/year if available (e.g., "2019 Honda Civic"), or nothing if not set
    - Photo thumbnail or a colored placeholder with the first letter of the vehicle name
    - No badge counts yet (Sprint 05)
  - Cards are tappable — navigate to vehicle detail
- Mobile: single column, cards are full-width with generous padding
- Desktop: 2 columns up to 768px, 3 columns above 1024px, max-width container ~1200px

**Actions available:**
- Tap a card → navigate to Vehicle Detail
- "+ Add Vehicle" button → open Add Vehicle form

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: Vehicle cards use a colored initial-letter placeholder when no photo is set, not a generic car icon.
> **Why**: Each vehicle should feel personal. A letter placeholder tied to the name (e.g., a bold "B" for "Blue Thunder") reinforces the companion feel. A generic car icon feels impersonal and identical across vehicles.
> **Alternatives considered**: Generic car silhouette icon, user-selectable emoji, car-type icons
> **Confidence**: High

### Add Vehicle Form

**What the user sees:**
- **Page title**: "Add a Vehicle"
- **Back arrow** in top bar to return to dashboard
- Form fields:
  1. **Name** — text input, required, placeholder: *"Give your vehicle a name"*, max 50 characters
  2. **Make** — text input, optional, placeholder: *"e.g., Honda"*, max 50 characters
  3. **Model** — text input, optional, placeholder: *"e.g., Civic"*, max 50 characters
  4. **Year** — numeric input, optional, placeholder: *"e.g., 2019"*, 4-digit year only (1900–current year + 1)
  5. **No photo upload here** — that's Sprint 06
- **Save button**: "Add to Garage" — full-width on mobile, right-aligned on desktop
- Below the name field: helper text *"This is what you'll see in your garage — pick something you'll recognize."*

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: Add Vehicle is a full page, not a modal/dialog.
> **Why**: On mobile, modals with form fields are awkward — keyboard covers the modal, scrolling is janky. A full page feels more intentional and gives the form room to breathe.
> **Alternatives considered**: Bottom-sheet modal, inline form on dashboard, dialog
> **Confidence**: High

### Vehicle Detail View

**What the user sees:**
- **Back arrow** in top bar → returns to dashboard
- **Vehicle profile header**:
  - Vehicle name (large, prominent)
  - Make / model / year below the name (if set), in lighter text
  - Photo or colored initial-letter placeholder (larger than the card version)
  - "Edit" link/button next to the name → opens edit mode
- **Tab bar** below the header: "Fuel" and "Maintenance" tabs
  - Both tabs show empty states in this sprint:
    - Fuel tab: *"No fill-ups yet. Log your first one to start tracking fuel economy."* with a disabled/grayed "Log Fill-up" button (enabled in Sprint 03)
    - Maintenance tab: *"No maintenance items yet. Set up your first one to stay on track."* with a disabled/grayed "Add Item" button (enabled in Sprint 04)
- Mobile: tabs are full-width, equal-split
- Desktop: tabs are left-aligned, auto-width

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: Tabs are rendered with empty states and disabled CTAs in Sprint 02, not hidden entirely.
> **Why**: Showing the tab structure early teaches the user what's coming and establishes the information architecture. Hiding tabs and adding them later changes the page layout between sprints, which is more disruptive to the user's mental model.
> **Alternatives considered**: Hide tabs until Sprint 03/04, show tabs with "Coming soon" labels
> **Confidence**: Medium — could argue for hiding, but showing is more honest about the app's shape.

### Edit Vehicle Profile (Inline Edit Mode)

**What the user sees:**
- Same Vehicle Detail page, but the profile header switches to edit mode:
  - Name becomes an editable text input (pre-filled with current name)
  - Make, model, year become editable inputs (pre-filled or empty)
  - Two buttons: "Save" (primary) and "Cancel" (secondary)
- The rest of the page (tabs) remains visible but non-interactive during edit
- Mobile: edit fields stack vertically, full-width
- Desktop: edit fields in a 2-column grid (name full-width, make/model/year in a row)

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: Inline editing on the vehicle detail page, not a separate "Edit Vehicle" page.
> **Why**: The user is already looking at the vehicle — switching to a separate page and back adds unnecessary navigation. Inline editing is fast and contextual.
> **Alternatives considered**: Separate edit page, modal dialog
> **Confidence**: High

---

## Interaction Details

### Add Vehicle Flow
1. User taps "+ Add Vehicle" on the dashboard
2. Navigates to Add Vehicle page
3. User fills in the name (required) and optional fields
4. Validation: name is required; if empty on submit, show inline error: *"Your vehicle needs a name"*
5. Year validation: must be 4 digits, between 1900 and current year + 1 (allow next model year). If invalid: *"Enter a valid year"*
6. On save: show saving spinner on button, disable form
7. Success: navigate to the new vehicle's detail page with a brief toast: *"[Vehicle name] added to your garage!"*
8. The dashboard now shows this vehicle in the card grid

### Edit Vehicle Flow
1. User taps "Edit" on the vehicle detail header
2. Profile header switches to edit mode (inputs replace text)
3. User modifies fields
4. "Save" → validate → show spinner → save → exit edit mode → toast: *"Changes saved"*
5. "Cancel" → discard changes, exit edit mode (no confirmation needed — changes are minor)

### Form Validation Rules
| Field | Required | Validation | Error Message |
|-------|----------|------------|---------------|
| Name  | Yes | 1–50 characters, non-blank | "Your vehicle needs a name" |
| Make  | No | 0–50 characters | "Make is too long" (if > 50) |
| Model | No | 0–50 characters | "Model is too long" (if > 50) |
| Year  | No | 4-digit number, 1900–(current year + 1) | "Enter a valid year" |

### Loading States
- Dashboard: skeleton cards (3 placeholder cards) while loading garage data
- Vehicle detail: skeleton header + tab placeholders while loading vehicle data
- Add vehicle: button shows spinner, form disabled during save

### Error States
- Save failure (network): inline error banner at the top of the form: *"Couldn't save — please try again."* Button re-enables.
- Load failure (vehicle detail): full-page error with retry button
- 404 (vehicle not found): *"This vehicle doesn't exist or has been removed."* with a "Back to garage" link

### Empty State Transitions
- When the user adds their first vehicle, the empty garage state smoothly transitions to the card grid. No jarring page reload — the new card appears.

---

## Edge Cases & Decisions

### Decision: Can two vehicles have the same name?
**Decision**: Yes. Names are personal labels, not unique identifiers. A user might have "Work Car" and name a second vehicle "Work Car" after selling the first.
**Confidence**: High.

### Decision: Vehicle card sort order
**Decision**: Most recently added first (reverse chronological by creation date). No manual reordering in v1.
**Confidence**: Medium — could also be alphabetical, but recency feels more natural for a journal-style app.

### Decision: Maximum number of vehicles
**Decision**: No hard limit in v1. Realistically, even 20 vehicles is an unusual case. The grid should handle it gracefully (just scroll).
**Confidence**: High.

### Decision: Vehicle name character restrictions
**Decision**: Allow any Unicode characters (letters, numbers, emoji). Trim leading/trailing whitespace. No special character restrictions — if someone wants to name their car "🚗 My Baby", that's fine.
**Confidence**: High.

### Edge case: User navigates to a vehicle that belongs to another user
API returns 404 (not 403) — don't leak that the vehicle exists. Frontend shows the "doesn't exist" message.

### Edge case: User submits the Add Vehicle form twice (double-click)
Disable the button on first click. The API should also be idempotent — use a client-generated command ID to deduplicate.

---

## Out of Scope

- Vehicle photos (Sprint 06)
- Fuel log entry (Sprint 03)
- Maintenance items (Sprint 04)
- Dashboard attention strip and badges (Sprint 05)
- Archiving/restoring/deleting vehicles (Sprint 06)
- Searching or filtering vehicles
- Manual card reordering

---

## Dependencies

- Sprint 01: Auth flow, nav shell, Aspire infrastructure, MartenDB setup

---

## Data Contracts

### Vehicle (read model — API → Frontend)
```
Vehicle {
  vehicleId: string (UUID)
  name: string
  make: string | null
  model: string | null
  year: number | null
  photoUrl: string | null       // always null in Sprint 02
  createdAt: Instant
}
```

### GarageSummary (API → Frontend)
```
GarageSummary {
  vehicles: Vehicle[]           // sorted by createdAt descending
}
```

### AddVehicleCommand (Frontend → API)
```
AddVehicleCommand {
  commandId: string (UUID)      // client-generated, for idempotency
  name: string                  // required, 1–50 chars
  make: string | null
  model: string | null
  year: number | null
}
```

### UpdateVehicleCommand (Frontend → API)
```
UpdateVehicleCommand {
  vehicleId: string (UUID)
  name: string                  // required, 1–50 chars
  make: string | null
  model: string | null
  year: number | null
}
```

### Domain Events
```
VehicleAdded {
  vehicleId: string
  userId: string
  name: string
  make: string | null
  model: string | null
  year: number | null
  occurredAt: Instant
}

VehicleUpdated {
  vehicleId: string
  name: string
  make: string | null
  model: string | null
  year: number | null
  occurredAt: Instant
}
```
