# Sprint 04 — Maintenance Tracking

> **Milestone 4 of 6** | Depends on: Sprint 03 (Fuel Logging — for odometer data)
> **Goal**: Users can define custom maintenance items for each vehicle, set mileage and/or time intervals with warning thresholds, record service events, and see the current status of each item.

---

## User Stories

### US-4.1: User defines a maintenance item
**As a** user, **I want to** create custom maintenance items for my vehicle (like "Oil Change" or "Tire Rotation") with mileage and/or time intervals **so that** the app knows when to remind me.

### US-4.2: User sees maintenance status
**As a** user, **I want to** see at a glance whether each maintenance item is OK, due soon, or overdue **so that** I know what needs attention.

### US-4.3: User records maintenance performed
**As a** user, **I want to** log that I performed maintenance **so that** the interval clock resets and the status updates.

### US-4.4: User edits a maintenance item
**As a** user, **I want to** change the intervals or thresholds on a maintenance item **so that** it stays accurate as my needs change.

### US-4.5: User views maintenance history
**As a** user, **I want to** see the history of service records for each maintenance item **so that** I can track what was done and when.

### US-4.6: User removes a maintenance item
**As a** user, **I want to** remove a maintenance item I no longer need **so that** it doesn't clutter my view.

### US-4.7: User edits or deletes a maintenance record
**As a** user, **I want to** correct or remove a past maintenance record **so that** my history stays accurate.

---

## Screen Descriptions

### Vehicle Detail — Maintenance Tab

**What the user sees:**
- Tab bar: "Maintenance" tab is active
- **"+ Add Item" button** at the top of the tab content
- **Maintenance items list**: each item is an expandable card showing:
  - **Item name** (e.g., "Oil Change") — prominently displayed
  - **Status indicator**: a colored dot or badge
    - 🟢 **OK** (green) — not due yet, outside warning threshold
    - 🟡 **Due Soon** (amber) — within the warning threshold
    - 🔴 **Overdue** (red) — past the interval
  - **Status detail line**: one of:
    - OK: *"Next due in ~2,300 mi or 2 months"*
    - Due Soon: *"Due in ~450 mi or 1 week"*
    - Overdue: *"Overdue by ~800 mi"* or *"Overdue by 3 weeks"*
  - **Last performed**: *"Last: Jan 15, 2026 at 42,000 mi"* — or *"Never performed"* if no records
  - **Tap to expand**: reveals the maintenance history log and action buttons

- **Expanded item view** (inline, not a new page):
  - **History log**: reverse chronological list of service records
    - Each record: date, odometer, cost (if entered), notes (if entered)
    - Each record has a "•••" menu with "Edit" and "Delete"
  - **"Log Service" button**: record that this maintenance was just performed
  - **"Edit Item" link**: edit the item definition (name, intervals, thresholds)
  - **"Remove Item" link** (red text): delete the maintenance item entirely

- **Sort order**: Overdue items first, then Due Soon, then OK. Within each group, alphabetical by name.

- Mobile: items are full-width cards, tappable to expand
- Desktop: items are in a single column, max-width ~800px

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: Maintenance items are sorted by urgency (overdue → due soon → ok), not alphabetically.
> **Why**: The most important items should be at the top. A user checking their maintenance tab wants to know "what needs attention?" first. Alphabetical order buries overdue items if they start with late-alphabet letters.
> **Alternatives considered**: Alphabetical, user-defined order, by next-due-date
> **Confidence**: High

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: Expanded view is inline (accordion), not a separate page.
> **Why**: The user wants quick glances at history and quick actions. Navigating to a separate page for each item adds friction. Accordion keeps context — you can see the item's status while viewing its history.
> **Alternatives considered**: Separate maintenance item detail page, modal overlay
> **Confidence**: High

### Add Maintenance Item Form (Full Page)

**What the user sees:**
- **Page title**: "Add Maintenance Item" with back arrow
- **Vehicle context**: "for Blue Thunder"
- Form fields:
  1. **Name** — text input, required. Placeholder: *"e.g., Oil Change, Tire Rotation"*. Max 100 characters.
  2. **Mileage Interval** — numeric input, optional. Placeholder: *"Every ___ miles"*. Helper text: *"Leave blank if this item is time-based only."*
  3. **Time Interval** — two fields:
     - Number input (integer) + dropdown: "months" (default "months")
     - Placeholder: *"Every ___ months"*
     - Helper text: *"Leave blank if this item is mileage-based only."*
  4. **Interval logic note**: displayed between the two interval fields: *"If you set both, the item will be due at whichever comes first."*
  5. **Warning Threshold section** — collapsible, labeled "When to warn me" with default values shown:
     - **Mileage warning**: numeric input, default 500. Label: *"Warn me ___ miles before due"*
     - **Time warning**: numeric input + "days" label, default 14. Label: *"Warn me ___ days before due"*
     - Helper text: *"We'll mark the item as 'due soon' when it's within these thresholds."*
  6. **Initial state section**: 
     - **Last performed date** — date picker, optional. Label: *"When was this last done?"*
     - **Odometer at last service** — numeric input, optional. Label: *"Odometer at last service"*
     - Helper text: *"If you know when this was last done, enter it here so we can track from the right starting point. If you're not sure, leave it blank — we'll start tracking from today."*

- **Save button**: "Add Item"

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: The "Add Item" form includes optional "last performed" fields for initial state.
> **Why**: When a user first sets up maintenance tracking, their oil was probably changed 3 months ago — not today. Without a way to seed the initial state, every item would show as "never performed" and potentially trigger false overdue warnings immediately. This avoids the "everything is overdue on day one" problem.
> **Alternatives considered**: Always start from today (simpler but inaccurate), separate "import history" flow
> **Confidence**: High

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: Warning thresholds are in a collapsible section, defaulted to 500 mi / 14 days.
> **Why**: Most users will be fine with the defaults. Showing the threshold fields prominently adds complexity to what should be a simple form. The collapsible section makes them accessible without cluttering the default experience.
> **Alternatives considered**: Always visible, separate settings page, hidden entirely with only defaults
> **Confidence**: High

### Log Service Form (Full Page)

**What the user sees:**
- **Page title**: "Log Service" with back arrow
- **Context**: "Oil Change for Blue Thunder"
- Form fields:
  1. **Date** — date picker, defaults to today, editable. No future dates.
  2. **Odometer** — numeric input, required. Helper text: *"Last reading: 45,230 mi"*
  3. **Cost** — currency input, optional. Placeholder: *"Cost (optional)"*
  4. **Notes** — text area, optional. Max 500 characters. Placeholder: *"Any notes? (optional)"*
- **Save button**: "Save Service Record"
- After saving: the interval clock resets from this date and odometer. The item's status recalculates.

### Edit Maintenance Item Form

- Same as "Add Maintenance Item" but pre-populated
- Title: "Edit Maintenance Item"
- Save button: "Save Changes"
- Does NOT show the "Initial state" section — that only appears when adding
- **"Remove Item"** link at the bottom (red text)

### Edit Maintenance Record Form

- Same as "Log Service" but pre-populated
- Title: "Edit Service Record"
- Save button: "Save Changes"
- **"Delete"** link at the bottom (red text)

---

## Interaction Details

### Add Maintenance Item Flow
1. User navigates to vehicle detail → Maintenance tab
2. Taps "+ Add Item"
3. Fills in name (required) and at least one interval (mileage or time)
4. Optionally adjusts warning thresholds (expand the section)
5. Optionally enters "last performed" data to seed initial state
6. Taps "Add Item" → spinner → success → navigate back to maintenance tab
7. New item appears in the list with correct status
8. Toast: *"Oil Change added"*

### Log Service Flow
1. User taps on a maintenance item to expand it
2. Taps "Log Service"
3. Fills in date, odometer, optional cost and notes
4. Taps "Save Service Record" → spinner → success → navigate back
5. Item status recalculates. The "last performed" line updates. History shows the new record.
6. Toast: *"Service recorded!"*

### Remove Maintenance Item Flow
1. User taps on a maintenance item → expand → "Remove Item"
2. **Confirmation dialog**: *"Remove 'Oil Change'? All service history for this item will be removed."* with "Remove" (red) and "Cancel"
3. On confirm: item disappears from the list. Toast: *"Oil Change removed"*

### Delete Maintenance Record Flow
1. User taps "•••" on a service record → "Delete"
2. **Confirmation dialog**: *"Delete this service record from Jan 15, 2026?"*
3. On confirm: record removed. Item status recalculates based on the next most recent record.
4. Toast: *"Service record deleted"*

### Status Calculation Logic

**Mileage-based status:**
- `estimatedCurrentMileage` = most recent odometer reading from any source (fuel log or maintenance record) + linear interpolation (see Sprint 05 for full estimation, but basic version here)
- `milesSinceLastService` = `estimatedCurrentMileage` - `odometerAtLastService`
- If `milesSinceLastService` >= `mileageInterval`: **Overdue**
- Else if `milesSinceLastService` >= `mileageInterval` - `mileageWarning`: **Due Soon**
- Else: **OK**

**Time-based status:**
- `timeSinceLastService` = today - `dateOfLastService`
- `timeInterval` in months, converted to days for comparison
- If `timeSinceLastService` >= `timeInterval`: **Overdue**
- Else if `timeSinceLastService` >= `timeInterval` - `timeWarning`: **Due Soon**
- Else: **OK**

**Combined ("whichever comes first"):**
- Calculate both mileage-based and time-based status
- The **worse** status wins: if mileage says OK but time says Due Soon, the item is Due Soon

**Never performed + no initial state:**
- If no service records and no initial state was set: item shows as **Due Soon** immediately (not overdue — give the user a chance to log it)
- Status detail: *"No service history — log your first one to start tracking"*

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: Items with no service history show as "Due Soon" (amber), not "Overdue" (red).
> **Why**: Showing everything as overdue on day one creates anxiety and alarm fatigue — the user just set it up and is already behind? That violates the "no anxiety" principle. "Due Soon" gently nudges without creating false urgency.
> **Alternatives considered**: Show as "OK" (hides that action is needed), show as "Overdue" (too aggressive), show as "Unknown" (a new state to explain)
> **Confidence**: High

### Form Validation Rules

**Add/Edit Maintenance Item:**
| Field | Required | Validation | Error Message |
|-------|----------|------------|---------------|
| Name | Yes | 1–100 characters | "Give this item a name" |
| Mileage Interval | Conditional | Positive integer, max 999,999 | "Enter a valid mileage interval" |
| Time Interval | Conditional | Positive integer, 1–120 months | "Enter a valid time interval (1–120 months)" |
| At least one interval | Yes | Mileage or time (or both) | "Set a mileage interval, time interval, or both" |
| Mileage Warning | No | Positive integer, must be < mileage interval | "Warning must be less than the interval" |
| Time Warning | No | Positive integer (days), must be < time interval in days | "Warning must be less than the interval" |

**Log/Edit Service Record:**
| Field | Required | Validation | Error Message |
|-------|----------|------------|---------------|
| Date | Yes | Not in the future | "Date can't be in the future" |
| Odometer | Yes | Positive integer, max 9,999,999 | "Enter a valid odometer reading" |
| Cost | No | Positive number, max 99,999.99 | "Enter a valid cost" |
| Notes | No | Max 500 characters | "Notes are too long" |

### Loading & Error States
- Maintenance tab: skeleton cards while loading
- Form save failure: inline error banner
- Delete failure: error toast
- Load failure: inline error with retry

---

## Edge Cases & Decisions

### Decision: Can a vehicle have duplicate maintenance item names?
**Decision**: Yes. A user might have "Tire Rotation — Front" and "Tire Rotation — Rear" or accidentally create duplicates. We don't restrict uniqueness.
**Confidence**: High.

### Decision: What happens to status when you edit an item's intervals?
**Decision**: Status immediately recalculates based on the new intervals and the most recent service record. If the user reduces the interval, the item might go from OK to Due Soon or Overdue.
**Confidence**: High.

### Decision: What happens when the last service record for an item is deleted?
**Decision**: The item falls back to the next most recent record. If there are no records left and no initial state, it behaves as "never performed" (Due Soon). The initial state seeded at creation time is stored as a separate property of the maintenance item, not as a service record — so it persists even if all service records are deleted.
**Confidence**: High.

### Decision: Time interval unit
**Decision**: Months only (not weeks or days) for the interval definition. Warning thresholds are in days for finer granularity.
**Why**: Maintenance schedules are almost always in months (every 3 months, every 6 months, every 12 months). Days would be unusual and confusing. Weeks are ambiguous (is "every 2 weeks" really a maintenance interval?).
**Confidence**: High.

### Edge case: User sets a mileage interval but the vehicle has no odometer readings
Status calculation can't determine mileage-based status. Show the time-based status only, with a note: *"Log a fill-up or service to start mileage tracking."*

### Edge case: User logs a service record with an odometer reading lower than the previous service
Allow it with a warning (same as fuel logs). Could be a correction or a different odometer reference point.

### Decision: Maintenance item order on the tab
**Decision**: Sorted by urgency: Overdue → Due Soon → OK. Within each group, alphabetical by name. This is a fixed sort — no user reordering in v1.
**Confidence**: High.

---

## Out of Scope

- Pre-loaded maintenance schedules (e.g., "Standard oil change every 5,000 mi") — PRD explicitly excludes
- Maintenance reminders via push/email notifications
- Cost tracking summaries or reports
- Dashboard integration (Sprint 05)
- Recurring service provider tracking
- Attachment/receipt photos

---

## Dependencies

- Sprint 02: Vehicle CRUD, vehicle detail view with tab structure
- Sprint 03: Fuel logging (provides odometer readings used for mileage estimation)

---

## Data Contracts

### MaintenanceItem (read model — API → Frontend)
```
MaintenanceItem {
  itemId: string (UUID)
  vehicleId: string (UUID)
  name: string
  mileageInterval: number | null      // miles between services
  timeIntervalMonths: number | null   // months between services
  mileageWarning: number              // default 500
  timeWarningDays: number             // default 14
  status: "ok" | "dueSoon" | "overdue"
  statusDetail: string                // human-readable status line
  lastPerformedDate: LocalDate | null
  lastPerformedOdometer: number | null
  records: MaintenanceRecord[]        // reverse chronological
  createdAt: Instant
}
```

### MaintenanceRecord (read model)
```
MaintenanceRecord {
  recordId: string (UUID)
  itemId: string (UUID)
  date: LocalDate
  odometer: number
  cost: number | null
  notes: string | null
  createdAt: Instant
}
```

### MaintenanceTabData (API → Frontend)
```
MaintenanceTabData {
  vehicleId: string
  items: MaintenanceItem[]            // sorted by urgency, then name
  lastOdometer: number | null         // most recent reading from any source
}
```

### DefineMaintenanceItemCommand (Frontend → API)
```
DefineMaintenanceItemCommand {
  commandId: string (UUID)
  vehicleId: string (UUID)
  name: string
  mileageInterval: number | null
  timeIntervalMonths: number | null
  mileageWarning: number              // default 500 if not specified
  timeWarningDays: number             // default 14 if not specified
  initialLastPerformedDate: LocalDate | null
  initialLastPerformedOdometer: number | null
}
```

### UpdateMaintenanceItemCommand (Frontend → API)
```
UpdateMaintenanceItemCommand {
  itemId: string (UUID)
  vehicleId: string (UUID)
  name: string
  mileageInterval: number | null
  timeIntervalMonths: number | null
  mileageWarning: number
  timeWarningDays: number
}
```

### RemoveMaintenanceItemCommand (Frontend → API)
```
RemoveMaintenanceItemCommand {
  itemId: string (UUID)
  vehicleId: string (UUID)
}
```

### RecordMaintenanceCommand (Frontend → API)
```
RecordMaintenanceCommand {
  commandId: string (UUID)
  itemId: string (UUID)
  vehicleId: string (UUID)
  date: LocalDate
  odometer: number
  cost: number | null
  notes: string | null
}
```

### UpdateMaintenanceRecordCommand (Frontend → API)
```
UpdateMaintenanceRecordCommand {
  recordId: string (UUID)
  itemId: string (UUID)
  vehicleId: string (UUID)
  date: LocalDate
  odometer: number
  cost: number | null
  notes: string | null
}
```

### DeleteMaintenanceRecordCommand (Frontend → API)
```
DeleteMaintenanceRecordCommand {
  recordId: string (UUID)
  itemId: string (UUID)
  vehicleId: string (UUID)
}
```

### Domain Events
```
MaintenanceItemDefined {
  itemId: string
  vehicleId: string
  userId: string
  name: string
  mileageInterval: number | null
  timeIntervalMonths: number | null
  mileageWarning: number
  timeWarningDays: number
  initialLastPerformedDate: LocalDate | null
  initialLastPerformedOdometer: number | null
  occurredAt: Instant
}

MaintenanceItemUpdated {
  itemId: string
  vehicleId: string
  name: string
  mileageInterval: number | null
  timeIntervalMonths: number | null
  mileageWarning: number
  timeWarningDays: number
  occurredAt: Instant
}

MaintenanceItemRemoved {
  itemId: string
  vehicleId: string
  occurredAt: Instant
}

MaintenancePerformed {
  recordId: string
  itemId: string
  vehicleId: string
  userId: string
  date: LocalDate
  odometer: number
  cost: number | null
  notes: string | null
  occurredAt: Instant
}

MaintenanceRecordUpdated {
  recordId: string
  itemId: string
  vehicleId: string
  date: LocalDate
  odometer: number
  cost: number | null
  notes: string | null
  occurredAt: Instant
}

MaintenanceRecordDeleted {
  recordId: string
  itemId: string
  vehicleId: string
  occurredAt: Instant
}
```
