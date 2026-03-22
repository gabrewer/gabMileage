# Sprint 02 — Garage & Vehicles

> **Milestone 2 of 6** | Depends on: Sprint 01 (Foundation & Auth)
> **Goal**: Users can add vehicles to their garage, see them as a card grid on the dashboard, drill into a vehicle's detail page, and edit vehicle profiles. The tab structure is established for future sprints.

---

## User Stories

### US-2.1: User adds a vehicle to their garage
**As a** user, **I want to** add a vehicle with just a name (and optional make/model/year) **so that** I can start tracking it immediately without friction.

### US-2.2: User sees their vehicles as a card grid
**As a** user with vehicles, **I want to** see all my vehicles displayed as cards on the dashboard **so that** I can quickly identify and access any of them.

### US-2.3: User drills into vehicle details
**As a** user, **I want to** tap a vehicle card to see its detail page **so that** I can manage that vehicle's profile and (in future sprints) its fuel and maintenance data.

### US-2.4: User edits a vehicle's profile
**As a** user, **I want to** edit my vehicle's name, make, model, and year inline on the detail page **so that** I can keep information accurate without extra navigation.

---

## Screen Descriptions

### Dashboard — Garage Card Grid

**What the user sees:**
- Top bar (from Sprint 01)
- **Garage section heading**: "Your Garage" (left) + "+ Add Vehicle" button (right)
- **Card grid**: one card per active vehicle, ordered most-recently-added first
  - Each card shows:
    - **Vehicle name** — bold, prominent (e.g., "Blue Thunder")
    - **Subtitle**: make/model/year if available (e.g., "2019 Honda Civic"), empty if not set
    - **Photo area**: colored circle with the first letter of the vehicle name, in a generated color based on the vehicle ID. Full photo support comes in Sprint 06.
    - No badge counts yet (Sprint 05)
  - Cards are tappable → navigate to vehicle detail
- **Mobile**: Single column, cards are full-width, 12px gap
- **Tablet (768–1024px)**: 2-column grid
- **Desktop (>1024px)**: 3-column grid, max-width container ~1200px

**Actions available:**
- Tap a card → Vehicle Detail page
- "+ Add Vehicle" → Add Vehicle page

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: Vehicle cards show a colored initial-letter circle, not a generic car icon, when no photo exists.
> **Why**: Initials + generated color give each vehicle a unique visual identity even before a photo is uploaded. A generic car silhouette makes every card look identical, undermining the "warm companion" feel. The color is deterministically derived from vehicleId so it's stable.
> **Alternatives considered**: Generic car icon, user-selectable emoji, vehicle-type silhouettes
> **Confidence**: High

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: Cards are sorted by creation date (newest first), not alphabetically.
> **Why**: In a journal-feel app, recency is more natural than alphabetical. The most recently added vehicle is likely the one the user is actively tracking. Alphabetical sorting buries new vehicles if they start with late-alphabet letters.
> **Alternatives considered**: Alphabetical, last-interacted-with, manual drag-to-reorder
> **Confidence**: Medium — could revisit if users have many vehicles and struggle to find one.

### Add Vehicle Page

**What the user sees:**
- **Top bar**: ← back arrow + page title "Add a Vehicle"
- Form fields (vertically stacked):
  1. **Name** — text input, required
     - Placeholder: *"Give your vehicle a name"*
     - Helper text below: *"This is what you'll see in your garage — pick something you'll recognize."*
     - Max 50 characters
  2. **Make** — text input, optional
     - Placeholder: *"e.g., Honda"*
     - Max 50 characters
  3. **Model** — text input, optional
     - Placeholder: *"e.g., Civic"*
     - Max 50 characters
  4. **Year** — numeric input, optional
     - Placeholder: *"e.g., 2019"*
     - 4-digit year only, 1900 – (current year + 1)
  5. **No photo upload** — comes in Sprint 06
- **Save button**: "Add to Garage"
  - Mobile: full-width
  - Desktop: right-aligned, normal button width
- Form max-width: ~520px, centered

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: Add Vehicle is a full page, not a modal or bottom sheet.
> **Why**: On mobile, modals with form fields are awkward — the keyboard covers the modal, scrolling is broken, and the experience feels cramped. A full page gives the form room to breathe and feels more intentional for an action that adds something permanent.
> **Alternatives considered**: Bottom sheet modal, dialog overlay, inline form on dashboard
> **Confidence**: High

### Vehicle Detail Page

**What the user sees:**
- **Top bar**: ← back arrow → dashboard
- **Profile header section**:
  - Large colored initial-letter circle (or photo in Sprint 06)
  - Vehicle name — large heading
  - Make / model / year — subtitle, lighter text (only shown if any are set)
  - "Edit" button/link — right of the name, opens inline edit mode
- **Tab bar** below the header: two tabs — **"Fuel"** and **"Maintenance"**
  - Both tabs show placeholder empty states in this sprint:
    - **Fuel tab**: *"No fill-ups yet. Log your first one to start tracking fuel economy."* — "Log Fill-up" button (grayed/disabled — enabled in Sprint 03)
    - **Maintenance tab**: *"No maintenance items defined. Add your first one to stay on track."* — "Add Item" button (grayed/disabled — enabled in Sprint 04)
- **Mobile**: Tabs span full width, equal split
- **Desktop**: Tabs left-aligned, auto-width with bottom border highlight on active tab
- Content area max-width: ~800px on desktop

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: Tabs are rendered in Sprint 02 with disabled CTAs and empty-state text, not hidden entirely.
> **Why**: Showing the tab structure teaches users the app's information architecture early. They can see what's coming. Hiding tabs and adding them in later sprints changes page layout unexpectedly. Disabled buttons with descriptive empty states set expectations clearly.
> **Alternatives considered**: Hide tabs until content exists, tabs with "Coming soon" badges
> **Confidence**: Medium — a case could be made for hiding, but progressive disclosure of structure is better than surprise rearrangement.

### Inline Edit Mode (Vehicle Profile)

**What the user sees:**
- Same vehicle detail page, but the profile header switches to edit mode:
  - Name → editable text input (pre-filled)
  - Make → editable text input (pre-filled or empty)
  - Model → editable text input (pre-filled or empty)
  - Year → editable numeric input (pre-filled or empty)
  - Two buttons below the fields: **"Save"** (primary) + **"Cancel"** (secondary/text)
- The tab area remains visible but non-interactive (dimmed slightly) during edit
- **Mobile**: fields stack vertically, full-width
- **Desktop**: Name full-width; Make, Model, Year in a 3-column row

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: Profile editing is inline on the detail page, not a separate "Edit Vehicle" page.
> **Why**: The user is already looking at the vehicle. Navigating away to edit and back adds unnecessary round-trips. Inline editing is fast, contextual, and keeps the user oriented.
> **Alternatives considered**: Separate edit page, modal dialog, bottom sheet
> **Confidence**: High

---

## Interaction Details

### Add Vehicle Flow
1. User taps "+ Add Vehicle" on dashboard → navigates to Add Vehicle page
2. Fills in name (required); optionally fills make, model, year
3. Taps "Add to Garage":
   - Button shows spinner, form inputs disable
   - API receives `AddVehicleCommand` with a client-generated `commandId` for idempotency
4. **Success**: Navigate to the new vehicle's detail page. Toast: *"[Name] added to your garage!"*
5. **Failure**: Inline error banner above the form: *"Couldn't save — please try again."* Button re-enables.
6. Dashboard now shows this vehicle in the card grid when the user navigates back.

### Edit Vehicle Flow
1. User taps "Edit" on the vehicle detail profile header
2. Header transitions to edit mode — text fields become inputs
3. User modifies any fields
4. "Save" → validate → spinner → save → exit edit mode. Toast: *"Changes saved"*
5. "Cancel" → revert to original values, exit edit mode. No confirmation needed for cancel — edits are minor.

### Validation Rules

| Field | Required | Validation | Error Message |
|-------|----------|------------|---------------|
| Name | Yes | 1–50 chars after trimming, non-blank | *"Your vehicle needs a name"* |
| Make | No | 0–50 chars | *"Make is too long"* (>50) |
| Model | No | 0–50 chars | *"Model is too long"* (>50) |
| Year | No | 4-digit integer, 1900–(current year + 1) | *"Enter a valid year"* |

- Validation fires on submit (not on blur, to avoid premature errors)
- Name is trimmed of leading/trailing whitespace before validation and storage
- All Unicode allowed in name (letters, numbers, emoji — if they want "🚗 Baby" that's fine)

### Loading States
- **Dashboard**: 3 skeleton cards while garage data loads. Transition smoothly to either empty state or populated grid — no flash of wrong state.
- **Vehicle detail**: Skeleton header + tab placeholders while vehicle data loads.
- **Add Vehicle save**: Button spinner, form disabled.

### Error States
- **Save failure** (Add or Edit): Inline error banner at form top: *"Couldn't save — please try again."* Form re-enables.
- **Vehicle detail 404**: *"This vehicle doesn't exist or has been removed."* + "Back to garage" link.
- **Vehicle detail load failure**: Full-page error with "Retry" button.
- **Dashboard load failure**: Inline error with "Retry" — do NOT show empty state for load errors.

### Empty → Populated Transition
When the user adds their first vehicle and navigates back to the dashboard, the empty state should transition to the card grid. Since navigation is involved (detail page → dashboard), this is a standard page load — no animation needed. The dashboard fetches fresh data on mount.

---

## Edge Cases & Decisions

### Decision: Can two vehicles have the same name?
**Decision**: Yes. Names are personal labels, not unique keys. A user might have "The Truck" for two different trucks over time.
**Confidence**: High.

### Decision: Maximum number of vehicles
**Decision**: No hard limit in v1. The grid scrolls. Even 20 vehicles is unusual for a personal garage app.
**Confidence**: High.

### Decision: Vehicle name character restrictions
**Decision**: Any Unicode characters allowed. Trim whitespace. No restrictions beyond length. Emoji are fine.
**Confidence**: High.

### Decision: What if a user tries to access another user's vehicle?
**Decision**: API returns 404 (not 403). Don't reveal that the vehicle exists to unauthorized users. Frontend shows the "doesn't exist" message.
**Confidence**: High.

### Decision: Double-submit prevention
**Decision**: Disable the "Add to Garage" button on click. Also include a client-generated `commandId` (UUID) in the command for server-side idempotency — if the same `commandId` arrives twice, the second is a no-op.
**Confidence**: High.

### Edge case: Very long vehicle name at exactly 50 characters
Display truncates with ellipsis on the card if it exceeds the card's text area. Full name shows on the detail page (no truncation — the heading wraps).

### Edge case: Year = current year + 1
Allowed. People buy next model-year vehicles before the calendar year (e.g., a "2027 Civic" bought in late 2026).

---

## Out of Scope

- Vehicle photos (Sprint 06)
- Fuel log entry (Sprint 03)
- Maintenance items (Sprint 04)
- Dashboard attention strip and badge counts (Sprint 05)
- Archiving, restoring, deleting vehicles (Sprint 06)
- Searching or filtering vehicles
- Manual card reordering
- Vehicle comparison

---

## Dependencies

- **Sprint 01**: Auth flow, navigation shell, YARP Gateway, Aspire infrastructure, MartenDB

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
  photoUrl: string | null         // always null until Sprint 06
  photoThumbnailUrl: string | null // always null until Sprint 06
  createdAt: Instant
}
```

### GarageSummary (API → Frontend)
```
GarageSummary {
  vehicles: Vehicle[]             // sorted by createdAt desc (newest first)
}
```

### AddVehicleCommand (Frontend → API)
```
AddVehicleCommand {
  commandId: string (UUID)        // client-generated for idempotency
  name: string                    // required, 1–50 chars trimmed
  make: string | null
  model: string | null
  year: number | null
}
```

### UpdateVehicleCommand (Frontend → API)
```
UpdateVehicleCommand {
  vehicleId: string (UUID)
  name: string                    // required, 1–50 chars trimmed
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
