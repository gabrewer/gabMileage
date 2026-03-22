# Sprint 06 — Vehicle Lifecycle, Photos & Polish

> **Milestone 6 of 6** | Depends on: Sprint 05 (Dashboard & Attention System)
> **Goal**: Complete the v1 feature set. Vehicles can be archived, restored, and permanently deleted. Vehicle photos bring personality. Final responsive polish ensures the app feels great on every screen size.

---

## User Stories

### US-6.1: User archives a vehicle
**As a** user, **I want to** archive a vehicle I'm no longer actively using **so that** it's out of my way but its history is preserved.

### US-6.2: User views archived vehicles
**As a** user, **I want to** see my archived vehicles separately **so that** I can restore or permanently delete them.

### US-6.3: User restores an archived vehicle
**As a** user, **I want to** restore an archived vehicle back to my active garage **so that** I can resume tracking it.

### US-6.4: User permanently deletes an archived vehicle
**As a** user, **I want to** permanently delete an archived vehicle **so that** it's gone for good.

### US-6.5: User adds a photo to a vehicle
**As a** user, **I want to** upload a photo of my vehicle **so that** it feels personal and I can quickly identify it in my garage.

### US-6.6: User changes or removes a vehicle photo
**As a** user, **I want to** update or remove my vehicle's photo **so that** it stays current.

---

## Screen Descriptions

### Vehicle Detail — Archive Action

**What the user sees:**
- On the vehicle detail page, a "•••" overflow menu in the top-right of the header (next to the "Edit" button)
- Menu items:
  - "Archive Vehicle" — the archive action
- This menu is subtle — not a prominent button. Archiving is an infrequent action.

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: Archive is in an overflow menu, not a visible button.
> **Why**: Archiving is rare — most users will never archive a vehicle. Giving it a prominent button wastes space and adds noise. The overflow menu keeps it discoverable without being distracting.
> **Alternatives considered**: Visible "Archive" button, settings page action, swipe-to-archive on card
> **Confidence**: High

### Archived Vehicles Section (Dashboard)

**What the user sees:**
- Below the active garage card grid, a collapsible section: **"Archived Vehicles"**
- **Only visible if the user has archived vehicles** — hidden otherwise
- Default state: **collapsed** (just the heading with a count, e.g., "Archived Vehicles (2)")
- Expanded state: a list (not a card grid — archived vehicles are secondary):
  - Each row shows:
    - Vehicle name
    - Make/model/year (if set)
    - Archived date: *"Archived Mar 10, 2026"*
    - Two actions: "Restore" button (outlined/secondary) and "Delete" button (red text)
- No attention badges on archived vehicles

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: Archived vehicles are in a collapsed section on the dashboard, not a separate page.
> **Why**: Keeping archived vehicles on the same page (but collapsed) means the user doesn't have to navigate to find them. The collapsed default ensures they don't clutter the primary view. A separate page would be over-engineering for what's likely 0–2 archived vehicles.
> **Alternatives considered**: Separate "Archived" page, bottom drawer, settings page
> **Confidence**: High

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: Archived vehicles are shown as a list, not cards.
> **Why**: Cards imply interactivity and importance. Archived vehicles are dormant — showing them as a simple list visually demotes them and reinforces that they're not actively tracked. It also saves space.
> **Alternatives considered**: Same card grid but dimmed, separate card layout
> **Confidence**: High

### Archive Confirmation Dialog

**What the user sees:**
- Dialog: *"Archive [Vehicle Name]?"*
- Body: *"[Vehicle Name] will be moved out of your active garage. All history will be preserved — you can restore it anytime."*
- Buttons: "Archive" (primary) and "Cancel"
- Tone: reassuring, not scary. This is not a delete.

### Restore Confirmation

**What the user sees:**
- No confirmation dialog for restore — it's a safe, reversible action.
- Tapping "Restore" immediately restores the vehicle.
- The vehicle card reappears in the active garage grid.
- Toast: *"[Vehicle Name] is back in your garage!"*

### Permanent Delete Confirmation Dialog

**What the user sees:**
- Dialog: *"Permanently delete [Vehicle Name]?"*
- Body: *"This will permanently delete [Vehicle Name] and all its fuel logs, maintenance history, and photos. This cannot be undone."*
- Confirmation input: user must type the vehicle name to confirm (for vehicles with data). If the vehicle has no fuel logs or maintenance records, skip the type-to-confirm.
- Buttons: "Delete Forever" (red, disabled until name is typed) and "Cancel"

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: Permanent delete requires typing the vehicle name for vehicles that have data (fuel logs or maintenance records).
> **Why**: This is an unrecoverable, destructive action that could destroy months of tracking history. The type-to-confirm pattern (used by GitHub, AWS, etc.) prevents accidental deletion. For empty vehicles (no data), the friction isn't warranted — a simple confirm is enough.
> **Alternatives considered**: Simple confirmation only, 24-hour grace period, no permanent delete in v1
> **Confidence**: High

### Vehicle Photo — Upload & Display

**Photo on Vehicle Detail:**
- Large photo in the profile header area (above the name)
- Aspect ratio: 16:9 cropped, with object-fit: cover
- If no photo: colored initial-letter placeholder (same as Sprint 02, but larger)
- **"Change Photo" button** overlaid on the photo (or placeholder) — a small camera icon button in the bottom-right corner
- Tapping "Change Photo" opens:
  - Option 1: "Upload Photo" — native file picker
  - Option 2: "Remove Photo" (only shown if a photo exists)

**Photo on Garage Card:**
- Thumbnail version of the same photo, also 16:9 cropped
- If no photo: colored initial-letter placeholder

**Photo constraints:**
- Max file size: 5 MB
- Accepted formats: JPEG, PNG, WebP
- Uploaded to Azure Blob Storage
- Resized server-side to two versions: thumbnail (400×225) and full (1200×675)
- EXIF data stripped for privacy

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: Photos are uploaded and resized server-side, not cropped client-side.
> **Why**: Client-side cropping adds significant UI complexity (crop tool, preview, aspect ratio enforcement). Server-side center-crop with object-fit: cover in CSS handles this much more simply. The user uploads any photo, we make it look good automatically.
> **Alternatives considered**: Client-side crop tool, user-selected crop region, square photos
> **Confidence**: High

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: 16:9 aspect ratio for vehicle photos.
> **Why**: Vehicles are horizontal objects — 16:9 matches the natural aspect of a car photo better than 1:1 (square) or 4:3. It also works well with card layouts on both mobile and desktop.
> **Alternatives considered**: 1:1 (square), 4:3, user-chosen aspect ratio
> **Confidence**: Medium — could also work as 3:2, but 16:9 is more cinematic and "car-like"

### Photo Upload Flow
1. User taps the camera icon on the vehicle detail photo area
2. Small action sheet / dropdown: "Upload Photo" and optionally "Remove Photo"
3. User selects "Upload Photo" → native file picker opens
4. User selects an image → upload begins
5. Show upload progress: overlay on the photo area with a progress bar
6. On success: photo refreshes. Toast: *"Photo updated!"*
7. On failure: error toast *"Couldn't upload — please try a smaller image or different format."*

### Remove Photo Flow
1. User taps camera icon → "Remove Photo"
2. No confirmation — it's a soft action (they can re-upload)
3. Photo area reverts to initial-letter placeholder
4. Toast: *"Photo removed"*

---

## Interaction Details

### Archive Flow
1. User is on a vehicle's detail page
2. Taps "•••" menu → "Archive Vehicle"
3. Confirmation dialog appears
4. User taps "Archive" → vehicle is archived
5. User is navigated back to the dashboard
6. Vehicle card is gone from the active grid
7. "Archived Vehicles" section appears (or updates its count)
8. Toast: *"[Vehicle Name] archived"*

### Restore Flow
1. User expands "Archived Vehicles" section on the dashboard
2. Taps "Restore" on an archived vehicle
3. Vehicle immediately moves back to the active garage grid (appears at the top — it's the most recently modified)
4. If the vehicle was the last archived one, the "Archived Vehicles" section disappears
5. Toast: *"[Vehicle Name] is back in your garage!"*
6. Maintenance statuses recalculate (the vehicle might have overdue items now)

### Permanent Delete Flow
1. User expands "Archived Vehicles" section
2. Taps "Delete" on an archived vehicle
3. Confirmation dialog with type-to-confirm (if vehicle has data)
4. User types the vehicle name → "Delete Forever" button enables
5. User taps "Delete Forever" → vehicle and all associated data are permanently removed
6. Vehicle row disappears from archived list
7. Toast: *"[Vehicle Name] permanently deleted"*

### Archived Vehicle Access
- Archived vehicles are **not accessible** for viewing details in v1. The user must restore before viewing.
- The archived list only shows name, metadata, and the archive date.

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: Archived vehicles cannot be viewed in detail — only restored or deleted.
> **Why**: Supporting a "read-only detail view" for archived vehicles adds complexity (disable all edit buttons, suppress the attention system, etc.) for a rare use case. If a user wants to see the data, they restore it (which is one click) and can re-archive after.
> **Alternatives considered**: Read-only detail view, full detail view with disabled editing
> **Confidence**: Medium — could revisit if users find this frustrating.

---

## Responsive Polish Checklist

This sprint includes a pass across all screens to ensure consistent, polished responsive behavior:

### Mobile (< 768px)
- [ ] All forms: fields stack vertically, full-width
- [ ] All buttons: full-width for primary actions
- [ ] Garage cards: single column
- [ ] Attention strip: horizontal scroll with snap
- [ ] Fuel log entries: compact card layout (date + odometer on one line, fuel details on next)
- [ ] Maintenance items: full-width accordion cards
- [ ] Back navigation: back arrow in top bar for all drill-down pages
- [ ] Touch targets: minimum 44px height for all interactive elements
- [ ] No horizontal scroll on any page except the attention strip

### Tablet (768px–1024px)
- [ ] Garage cards: 2 columns
- [ ] Attention strip: 2-column wrapped grid
- [ ] Forms: max-width ~600px, centered
- [ ] Vehicle detail: content max-width ~800px, centered

### Desktop (> 1024px)
- [ ] Garage cards: 3 columns
- [ ] Attention strip: 3-column wrapped grid
- [ ] Forms: max-width ~600px, centered
- [ ] Vehicle detail: content max-width ~900px, centered
- [ ] Overall page max-width: ~1200px, centered with side padding

### Cross-cutting
- [ ] Toast notifications: bottom-center on mobile, bottom-right on desktop. Auto-dismiss after 4 seconds.
- [ ] Loading skeletons: consistent style across all pages (same border radius, animation, colors)
- [ ] Error states: consistent pattern — inline banner for form errors, full-page with retry for load failures
- [ ] Empty states: consistent illustration style and text formatting
- [ ] Date formatting: "Mar 15, 2026" everywhere (short month, no leading zeros)
- [ ] Number formatting: commas for thousands separators on odometer values
- [ ] Currency: "$" prefix, 2 decimal places for costs, 3 for price/gal
- [ ] Tilde (~) prefix for all estimated mileage values

---

## Edge Cases & Decisions

### Decision: Can you permanently delete an active (non-archived) vehicle?
**Decision**: No. You must archive first, then delete. This two-step process acts as a safety net.
**Confidence**: High.

### Decision: What happens to attention strip when a vehicle is archived?
**Decision**: All attention items for that vehicle immediately disappear from the strip. Dashboard badge counts update. This is expected — the user explicitly chose to stop tracking this vehicle.
**Confidence**: High.

### Decision: Does archiving preserve the event history?
**Decision**: Yes. Archiving sets an `isArchived` flag. All events remain in the event store. Restoring simply clears the flag. Only permanent delete removes the events (marks the stream as deleted in Marten).
**Confidence**: High.

### Decision: Photo uploads during poor connectivity
**Decision**: Show a progress bar. If the upload fails, show an error toast. No auto-retry. The user can try again manually.
**Confidence**: High.

### Edge case: User uploads a very large image (e.g., 4.9 MB JPEG)
Server-side resizing handles this. The original is not stored — only the resized versions (thumbnail + full). This keeps storage costs reasonable.

### Edge case: User archives a vehicle, then another user in a different session sees stale data
Not applicable — multi-user is not in v1. Each user has their own garage. If the same user has two tabs open, the other tab will see stale data until refreshed. This is acceptable for v1.

### Edge case: Can you delete the only vehicle in the garage?
Yes. After deletion, the dashboard shows the empty garage state. No restriction on deleting the last vehicle.

---

## Out of Scope

- Bulk archive/delete
- Archive date filters
- Photo galleries (multiple photos per vehicle)
- Photo editing (crop, rotate, filter)
- Drag-and-drop photo upload
- Vehicle transfer between users
- Export vehicle data before deletion
- Undo permanent delete

---

## Dependencies

- Sprint 01: Auth, nav shell
- Sprint 02: Vehicle CRUD, garage card grid, vehicle detail view
- Sprint 03: Fuel logging (data integrity during archive/delete)
- Sprint 04: Maintenance tracking (data integrity during archive/delete)
- Sprint 05: Dashboard attention system (remove archived vehicles from attention)
- Azure Blob Storage: must be configured for photo uploads

---

## Data Contracts

### Vehicle (enhanced read model — API → Frontend)
```
Vehicle {
  vehicleId: string (UUID)
  name: string
  make: string | null
  model: string | null
  year: number | null
  photoUrl: string | null            // URL to the full-size photo
  photoThumbnailUrl: string | null   // URL to the thumbnail photo
  isArchived: boolean
  archivedAt: Instant | null
  createdAt: Instant
}
```

### ArchivedVehicle (API → Frontend)
```
ArchivedVehicle {
  vehicleId: string (UUID)
  name: string
  make: string | null
  model: string | null
  year: number | null
  archivedAt: Instant
  hasData: boolean                   // true if vehicle has any fuel logs or maintenance records
}
```

### GarageSummary (enhanced — API → Frontend)
```
GarageSummary {
  vehicles: VehicleCard[]            // active only
  archivedVehicles: ArchivedVehicle[] // archived only, sorted by archivedAt desc
}
```

### ArchiveVehicleCommand (Frontend → API)
```
ArchiveVehicleCommand {
  vehicleId: string (UUID)
}
```

### RestoreVehicleCommand (Frontend → API)
```
RestoreVehicleCommand {
  vehicleId: string (UUID)
}
```

### DeleteVehicleCommand (Frontend → API)
```
DeleteVehicleCommand {
  vehicleId: string (UUID)
  confirmationName: string           // must match vehicle name
}
```

### UploadVehiclePhotoCommand (Frontend → API)
```
// This is a multipart form upload, not a JSON command
UploadVehiclePhoto {
  vehicleId: string (UUID)           // in the URL path
  photo: File                        // multipart file upload
}

Response: {
  photoUrl: string
  photoThumbnailUrl: string
}
```

### RemoveVehiclePhotoCommand (Frontend → API)
```
RemoveVehiclePhotoCommand {
  vehicleId: string (UUID)
}
```

### Domain Events
```
VehicleArchived {
  vehicleId: string
  userId: string
  occurredAt: Instant
}

VehicleRestored {
  vehicleId: string
  userId: string
  occurredAt: Instant
}

VehicleDeleted {
  vehicleId: string
  userId: string
  occurredAt: Instant
}

VehiclePhotoUploaded {
  vehicleId: string
  photoUrl: string
  photoThumbnailUrl: string
  occurredAt: Instant
}

VehiclePhotoRemoved {
  vehicleId: string
  occurredAt: Instant
}
```
