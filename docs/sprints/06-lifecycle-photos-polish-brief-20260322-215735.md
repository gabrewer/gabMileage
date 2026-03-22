# Sprint 06 — Vehicle Lifecycle, Photos & Polish

> **Milestone 6 of 6** | Depends on: Sprint 05 (Dashboard & Attention System)
> **Goal**: Complete the v1 feature set. Vehicles can be archived, restored, and permanently deleted. Vehicle photos add personality. A final responsive polish pass ensures the app feels great on every screen size.

---

## User Stories

### US-6.1: User archives a vehicle
**As a** user, **I want to** archive a vehicle I'm no longer actively tracking **so that** it's out of my daily view but its history is preserved for future reference.

### US-6.2: User views archived vehicles
**As a** user, **I want to** see my archived vehicles in a separate section **so that** I can restore or permanently delete them when needed.

### US-6.3: User restores an archived vehicle
**As a** user, **I want to** restore an archived vehicle to my active garage **so that** I can resume tracking maintenance and fuel.

### US-6.4: User permanently deletes an archived vehicle
**As a** user, **I want to** permanently delete an archived vehicle and all its data **so that** it's gone for good.

### US-6.5: User uploads a photo for a vehicle
**As a** user, **I want to** add a photo of my vehicle **so that** my garage feels personal and I can quickly identify each vehicle visually.

### US-6.6: User changes or removes a vehicle photo
**As a** user, **I want to** update or remove my vehicle's photo **so that** it stays current.

---

## Screen Descriptions

### Vehicle Detail — Archive Action

**What the user sees:**
- On the vehicle detail page, a **"⋯" overflow menu** in the top-right of the profile header (near the "Edit" button)
- Menu items:
  - "Archive Vehicle"
- The overflow menu is subtle — archiving is infrequent

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: Archive is in an overflow "⋯" menu, not a visible button.
> **Why**: Archiving is a rare action — most users may never use it. Giving it a prominent button wastes prime real estate and adds visual noise. The overflow menu makes it discoverable but unobtrusive.
> **Alternatives considered**: Visible "Archive" button, settings page, swipe-to-archive on card
> **Confidence**: High

### Archive Confirmation Dialog

**What the user sees:**
- **Title**: *"Archive [Vehicle Name]?"*
- **Body**: *"[Vehicle Name] will be removed from your active garage. All history is preserved — you can restore it anytime."*
- **Buttons**: "Archive" (primary, not red — this isn't destructive) + "Cancel"
- **Tone**: reassuring. Archiving is reversible.

### Dashboard — Archived Vehicles Section

**What the user sees:**
- Below the active garage card grid, a collapsible section: **"Archived Vehicles (N)"**
- **Only visible if archived vehicles exist** — hidden when there are none
- **Default state**: collapsed (just the heading + count + chevron)
- **Expanded state**: a simple list (not a card grid — archived vehicles are secondary citizens):
  - Each row shows:
    - Vehicle name (bold)
    - Make/model/year (lighter text, if set)
    - Archived date: *"Archived Mar 10, 2026"*
    - **"Restore"** button (outlined/secondary style)
    - **"Delete"** button (red text, not a filled button)
  - No photos, no badges — this is a management list, not a showcase
- Mobile: rows are full-width, actions on a second line below the vehicle info
- Desktop: single-row layout with actions right-aligned

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: Archived vehicles live in a collapsed section on the dashboard, not a separate page.
> **Why**: A separate page adds navigation complexity for what's likely 0–2 archived vehicles. A collapsed section on the main dashboard keeps them accessible without cluttering the primary view. The collapsed default ensures they never distract from the active garage.
> **Alternatives considered**: Separate "Archived" page, user menu option, settings page section
> **Confidence**: High

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: Archived vehicles are a list, not cards.
> **Why**: Cards imply interactivity and importance. Archived vehicles are dormant — showing them as a plain list visually demotes them, reinforcing that they're not actively tracked. It also saves vertical space.
> **Alternatives considered**: Dimmed card grid, separate card layout, table view
> **Confidence**: High

### Restore Flow

**What the user sees:**
- No confirmation dialog for restore — it's safe and reversible
- Tapping "Restore" immediately restores the vehicle
- Vehicle card reappears in the active garage grid (at the top — most recently modified)
- If the vehicle was the last archived one, the "Archived Vehicles" section disappears
- Toast: *"[Vehicle Name] is back in your garage!"*
- **Important**: maintenance statuses recalculate on restore — the vehicle may now have overdue items that appear in the attention strip

### Permanent Delete Confirmation Dialog

**What the user sees:**

**For vehicles WITH data** (any fuel logs or maintenance records):
- **Title**: *"Permanently delete [Vehicle Name]?"*
- **Body**: *"This will permanently delete [Vehicle Name] and all its fuel logs, maintenance history, and photos. This cannot be undone."*
- **Type-to-confirm input**: *"Type the vehicle name to confirm"* — user must type the exact vehicle name (case-insensitive match)
- **"Delete Forever"** button: red, disabled until the name is correctly typed
- "Cancel" button

**For vehicles WITHOUT data** (no logs, no maintenance — just the profile):
- **Title**: *"Permanently delete [Vehicle Name]?"*
- **Body**: *"This vehicle has no logs or history. Deletion is permanent and cannot be undone."*
- **"Delete Forever"** button: red, immediately clickable (no type-to-confirm needed)
- "Cancel" button

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: Type-to-confirm for vehicles with data; simple confirm for empty vehicles.
> **Why**: Permanent deletion of months of tracking history is unrecoverable — the type-to-confirm friction (used by GitHub, AWS) prevents catastrophic mistakes. But for empty vehicles (no real data loss), the extra friction is unwarranted — a simple confirm suffices.
> **Alternatives considered**: Always type-to-confirm, simple confirm only, 24-hour grace period, no permanent delete in v1
> **Confidence**: High

### Vehicle Photo — Upload & Display

**Photo on Vehicle Detail Page:**
- Large photo in the profile header area, above the vehicle name
- Aspect ratio: **16:9** cropped, `object-fit: cover`
- If no photo: colored initial-letter placeholder (same as cards, but larger — ~200px height on mobile, ~250px on desktop)
- **Camera icon button** (small, circular, translucent dark overlay) in the bottom-right corner of the photo area
- Tapping the camera icon reveals an action menu:
  - "Upload Photo" — opens native file picker
  - "Remove Photo" — only shown if a photo currently exists

**Photo on Garage Card:**
- Thumbnail version of the same photo, 16:9 cropped, `object-fit: cover`
- If no photo: colored initial-letter placeholder (as established in Sprint 02)

**Photo Constraints:**
- Max file size: **5 MB**
- Accepted formats: JPEG, PNG, WebP
- Uploaded to Azure Blob Storage via the API
- Server-side processing:
  - Resize to two versions: **thumbnail** (400×225) and **full** (1200×675)
  - Center-crop to 16:9 aspect ratio
  - Strip EXIF data (privacy — removes GPS coordinates, device info)
  - Original file is NOT stored after processing
- Photos served via CDN-friendly URLs with cache headers

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: Server-side center-crop, no client-side crop tool.
> **Why**: A client-side crop tool (draggable crop region, zoom, preview) is significant UI complexity. Server-side center-crop + CSS `object-fit: cover` produces good results for 95% of car photos (which are naturally horizontal and centered on the vehicle). If a specific crop is important, the user can pre-crop before uploading. Simplicity wins for v1.
> **Alternatives considered**: Client-side crop widget, user-adjustable focal point
> **Confidence**: High

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: 16:9 aspect ratio for vehicle photos.
> **Why**: Vehicles are horizontal objects — a car photo naturally fits 16:9 better than 1:1 or 4:3. 16:9 also works beautifully in card layouts on both mobile and desktop, giving each card a cinematic, premium feel.
> **Alternatives considered**: 1:1 (square), 4:3, 3:2
> **Confidence**: Medium — 3:2 would also work, but 16:9 is more distinctive

### Photo Upload Flow
1. User taps the camera icon on the vehicle detail photo area
2. Action menu appears: "Upload Photo" (and "Remove Photo" if one exists)
3. User selects "Upload Photo" → native file picker opens
4. User selects an image file
5. **Validation** (client-side, before upload):
   - File size ≤ 5 MB. If too large: toast *"Image must be 5 MB or smaller."*
   - File type is JPEG, PNG, or WebP. If wrong type: toast *"Please use a JPEG, PNG, or WebP image."*
6. Upload begins — show a progress overlay on the photo area:
   - Semi-transparent dark overlay with a circular progress indicator
   - Photo area still shows the current photo (or placeholder) underneath
7. **Success**: New photo loads in. Toast: *"Photo updated!"*
8. **Failure**: Error toast *"Couldn't upload photo — please try again."* Overlay disappears.

### Remove Photo Flow
1. User taps camera icon → "Remove Photo"
2. No confirmation — removing a photo is low-stakes (can re-upload anytime)
3. Photo area immediately reverts to the initial-letter placeholder
4. Toast: *"Photo removed"*

---

## Interaction Details

### Archive Flow
1. User is on a vehicle's detail page
2. Taps "⋯" overflow menu → "Archive Vehicle"
3. Confirmation dialog appears (reassuring tone)
4. User taps "Archive" → spinner → vehicle is archived
5. **Navigate to dashboard** — user doesn't stay on a now-archived vehicle
6. Vehicle card is gone from the active grid
7. "Archived Vehicles" section appears (or updates count)
8. Attention strip removes any items for this vehicle
9. Toast: *"[Vehicle Name] archived"*

### Restore Flow
1. User expands "Archived Vehicles" on dashboard
2. Taps "Restore" on a vehicle
3. Vehicle immediately moves to the active garage grid (newest first position)
4. If it was the last archived vehicle, the section disappears
5. Dashboard attention strip and badges recalculate — vehicle may have overdue items
6. Toast: *"[Vehicle Name] is back in your garage!"*

### Permanent Delete Flow
1. User expands "Archived Vehicles"
2. Taps "Delete" on a vehicle
3. Confirmation dialog appears (with or without type-to-confirm based on data presence)
4. For data-bearing vehicles: user types vehicle name → "Delete Forever" enables
5. User taps "Delete Forever" → spinner → vehicle and all data permanently removed
6. Vehicle row disappears. If last archived vehicle, section disappears.
7. Toast: *"[Vehicle Name] permanently deleted"*

### Archived Vehicle Access Policy
- **Archived vehicles cannot be viewed in detail** in v1. The detail page is only for active vehicles.
- To view data, the user must restore first (one click), then can re-archive after viewing.
- The archived list shows only: name, make/model/year, archived date, and actions.

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: No read-only detail view for archived vehicles — must restore to view.
> **Why**: Supporting a read-only view requires disabling all edit buttons, suppressing the attention system, handling the "archived" state in every component, etc. This is significant complexity for a rare use case ("I want to look at data I archived"). Restore is one click, and the user can re-archive immediately.
> **Alternatives considered**: Read-only detail view, full detail with disabled editing
> **Confidence**: Medium — revisit if users find the restore-to-view workaround frustrating.

---

## Responsive Polish Checklist

This sprint includes a comprehensive responsive audit across all screens:

### Mobile (< 768px)
- [ ] All form fields: stack vertically, full-width inputs
- [ ] All primary action buttons: full-width
- [ ] Garage card grid: single column
- [ ] Attention strip: horizontal scroll with `scroll-snap-type: x mandatory`
- [ ] Fuel log entries: compact card (date + odometer on line 1, fuel details on line 2)
- [ ] Maintenance items: full-width accordion cards
- [ ] Back navigation: ← arrow in top bar on all drill-down pages
- [ ] Touch targets: minimum 44×44px for all interactive elements
- [ ] No horizontal scroll on any page except the attention strip
- [ ] Date picker: native mobile date picker (`<input type="date">` or equivalent)

### Tablet (768–1024px)
- [ ] Garage cards: 2-column grid
- [ ] Attention strip: 2-column wrapped grid
- [ ] Forms: max-width ~520px, horizontally centered
- [ ] Vehicle detail content: max-width ~800px, centered

### Desktop (> 1024px)
- [ ] Garage cards: 3-column grid
- [ ] Attention strip: 3-column wrapped grid
- [ ] Forms: max-width ~520px, centered
- [ ] Vehicle detail content: max-width ~900px, centered
- [ ] Overall page content: max-width ~1200px, centered with side padding

### Cross-Cutting Consistency
- [ ] **Toasts**: bottom-center on mobile, bottom-right on desktop. Auto-dismiss after 4 seconds. Tap to dismiss. Max 1 toast visible at a time (new toast replaces old).
- [ ] **Loading skeletons**: consistent style — same border-radius (8px), same animation (pulse), same gray colors across all pages.
- [ ] **Error patterns**: inline banner for form save errors (above form), full-page with retry for load failures, toast for background action failures.
- [ ] **Empty states**: consistent illustration style, heading + subtext + CTA pattern.
- [ ] **Date formatting**: "Mar 15, 2026" everywhere — short month name, no leading zeros on day.
- [ ] **Odometer formatting**: commas for thousands (e.g., "45,230 mi").
- [ ] **Currency**: "$" prefix, 2 decimal places for costs, 3 for price/gal.
- [ ] **Tilde (~)**: prefix on all estimated mileage values.
- [ ] **Confirmation dialogs**: consistent structure — title, body, primary action + Cancel. Red primary for destructive actions.

---

## Edge Cases & Decisions

### Decision: Can you permanently delete an active (non-archived) vehicle?
**Decision**: No. Must archive first, then delete from the archived list. This two-step process is a safety net — it separates "I don't need this right now" (archive) from "I want this gone forever" (delete).
**Confidence**: High.

### Decision: What happens to the attention strip when a vehicle is archived?
**Decision**: All attention items for that vehicle immediately disappear. Badge counts update. This is expected — the user chose to stop tracking this vehicle.
**Confidence**: High.

### Decision: Does archiving preserve event history?
**Decision**: Yes. Archiving sets an `isArchived` flag on the vehicle aggregate. All events remain in the Marten event store. Restoring clears the flag and recalculates projections. Only permanent delete marks the event stream as deleted.
**Confidence**: High.

### Decision: Photo upload failure handling
**Decision**: Show error toast. No auto-retry. User can try again manually. No optimistic UI — don't show the photo until upload succeeds.
**Confidence**: High.

### Decision: Where does "Archive" live — vehicle detail or card?
**Decision**: Vehicle detail page only (in the overflow menu). Not on the card (too easy to accidentally trigger) and not on a settings page (too hidden).
**Confidence**: High.

### Edge case: Upload a very large image (e.g., 4.9 MB JPEG)
Server-side resize handles this. Original is not stored — only thumbnail (400×225) and full (1200×675). Storage cost stays reasonable.

### Edge case: Can you delete the last/only vehicle?
Yes. After deletion, the dashboard shows the empty garage state. No restriction.

### Edge case: Restored vehicle has overdue maintenance
Expected behavior: the attention strip and badge immediately reflect the overdue items. The user chose to bring it back — they should see its current state.

### Edge case: Archive, restore, re-archive
All supported. The `archivedAt` timestamp updates to the most recent archive date. History is fully preserved through all state changes.

### Edge case: Delete while another tab has the vehicle open
The detail page will get a 404 on next API call (the vehicle no longer exists). Show the "doesn't exist" error page with "Back to garage" link.

---

## Out of Scope

- Bulk archive or delete
- Photo galleries (multiple photos per vehicle)
- Client-side photo crop/edit/rotate
- Drag-and-drop photo upload
- Vehicle data export before deletion
- Undo permanent delete
- Vehicle transfer between users
- Archive date filtering or search
- Dark mode (future enhancement)
- Accessibility audit (should be ongoing, but a formal WCAG audit is post-v1)

---

## Dependencies

- **Sprint 01**: Auth, navigation shell
- **Sprint 02**: Vehicle CRUD, garage card grid, vehicle detail view
- **Sprint 03**: Fuel logging (data integrity during archive/delete)
- **Sprint 04**: Maintenance tracking (data integrity during archive/delete, status recalculation on restore)
- **Sprint 05**: Dashboard attention system (strip/badges must exclude archived vehicles, recalculate on archive/restore)
- **Azure Blob Storage**: must be configured and accessible for photo uploads

---

## Data Contracts

### Vehicle (final read model — API → Frontend)
```
Vehicle {
  vehicleId: string (UUID)
  name: string
  make: string | null
  model: string | null
  year: number | null
  photoUrl: string | null               // URL to full-size photo (1200×675)
  photoThumbnailUrl: string | null      // URL to thumbnail (400×225)
  isArchived: boolean
  archivedAt: Instant | null
  createdAt: Instant
}
```

### ArchivedVehicle (API → Frontend, for the archived list)
```
ArchivedVehicle {
  vehicleId: string (UUID)
  name: string
  make: string | null
  model: string | null
  year: number | null
  archivedAt: Instant
  hasData: boolean                      // true if any fuel logs or maintenance records exist
}
```

### GarageSummary (final — API → Frontend)
```
GarageSummary {
  vehicles: VehicleCard[]               // active only, sorted by createdAt desc
  archivedVehicles: ArchivedVehicle[]   // sorted by archivedAt desc
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
  confirmationName: string              // must match vehicle name (case-insensitive)
}
```

### UploadVehiclePhoto (Frontend → API)
```
// Multipart form upload, not JSON
POST /api/vehicles/{vehicleId}/photo
Content-Type: multipart/form-data
Body: photo file

Response {
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
