# Sprint 04 — Maintenance Tracking

> **Milestone 4 of 6** | Depends on: Sprint 03 (Fuel Logging — provides odometer data)
> **Goal**: Users can define custom maintenance items with mileage and/or time intervals, set warning thresholds, record service events that reset the clock, and see at-a-glance status (OK / Due Soon / Overdue) for each item.

---

## User Stories

### US-4.1: User defines a maintenance item
**As a** user, **I want to** create a custom maintenance item (like "Oil Change") with mileage and/or time intervals **so that** the app knows when service is coming due.

### US-4.2: User sees maintenance status at a glance
**As a** user, **I want to** see each maintenance item colored as OK (green), Due Soon (amber), or Overdue (red) **so that** I know what needs attention instantly.

### US-4.3: User records maintenance performed
**As a** user, **I want to** log that I performed maintenance with date and odometer **so that** the interval clock resets and status updates automatically.

### US-4.4: User edits a maintenance item definition
**As a** user, **I want to** adjust intervals, thresholds, or the name of a maintenance item **so that** it stays accurate as my needs evolve.

### US-4.5: User views maintenance history per item
**As a** user, **I want to** see the history of past service records for each maintenance item **so that** I have a complete service log.

### US-4.6: User removes a maintenance item
**As a** user, **I want to** remove a maintenance item I no longer need **so that** my view stays clean and relevant.

### US-4.7: User edits or deletes a past service record
**As a** user, **I want to** fix or remove a service record **so that** my maintenance history stays accurate.

---

## Screen Descriptions

### Vehicle Detail — Maintenance Tab

**What the user sees:**
- **Tab bar**: "Maintenance" tab active
- **"+ Add Item" button**: top of tab content
- **Maintenance item list**: each item is an expandable accordion card:

  **Collapsed state** (default):
  - **Status dot**: colored circle — 🟢 green (OK), 🟡 amber (Due Soon), 🔴 red (Overdue)
  - **Item name**: bold (e.g., "Oil Change")
  - **Status detail line**: one of:
    - OK: *"Next due in ~2,300 mi or 2 months"*
    - Due Soon: *"Due in ~450 mi or 1 week"*
    - Overdue: *"Overdue by ~800 mi"* or *"Overdue by 3 weeks"*
  - **Last performed**: *"Last: Jan 15, 2026 at 42,000 mi"* — or *"Never performed"*
  - Chevron/caret (▸) indicating expandable

  **Expanded state** (tap to toggle):
  - **Service history log**: reverse chronological list of records
    - Each record shows: date, odometer, cost (if entered), notes (if entered)
    - Each record has a "⋯" menu with "Edit" and "Delete"
  - **Action buttons**:
    - **"Log Service"** — primary button, records new maintenance
    - **"Edit Item"** — text link, edit the item definition
    - **"Remove Item"** — red text link, deletes the item and all its history

- **Sort order**: Overdue first → Due Soon → OK. Within each group, alphabetical by name.
- **Mobile**: Full-width accordion cards
- **Desktop**: Single column, max-width ~800px

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: Maintenance items use an accordion (expand/collapse) pattern, not separate detail pages.
> **Why**: Quick glances and quick actions are the goal. Navigating to a separate page per item adds friction for what should be a scan-and-act workflow. The accordion keeps all items visible and lets the user expand just the one they care about.
> **Alternatives considered**: Separate item detail page, modal overlay, always-expanded list
> **Confidence**: High

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: Items sorted by urgency (overdue → due soon → ok), not alphabetically.
> **Why**: The question "what needs attention?" is always more important than "what's in alphabetical order?" Urgency sorting means the answer is always at the top. Alphabetical buries an overdue "Tire Rotation" below an OK "Air Filter."
> **Alternatives considered**: Alphabetical, next-due-date, user-defined
> **Confidence**: High

### Add Maintenance Item Page

**What the user sees:**
- **Top bar**: ← back + "Add Maintenance Item"
- **Vehicle context**: "for Blue Thunder"
- **Form fields**:

  1. **Name** — text input, required
     - Placeholder: *"e.g., Oil Change, Tire Rotation"*
     - Max 100 characters

  2. **Mileage Interval** — numeric input, optional
     - Placeholder: *"Every ___ miles"*
     - Helper: *"Leave blank if time-based only."*

  3. **Time Interval** — number input + fixed "months" unit
     - Placeholder: *"Every ___ months"*
     - Helper: *"Leave blank if mileage-based only."*
     - Displayed between the two interval fields: *"If you set both, we'll track whichever comes first."*

  4. **Warning Thresholds** — collapsible section, labeled **"When to warn me"**
     - Default collapsed, shows "Defaults: 500 mi / 14 days" as summary
     - Expanded:
       - **Miles before due**: numeric, default 500. Label: *"Warn me ___ miles before due"*
       - **Days before due**: numeric, default 14. Label: *"Warn me ___ days before due"*
       - Helper: *"We'll show this item as 'due soon' when it's within these thresholds."*

  5. **Initial State** — section labeled **"Starting point"**
     - **Last performed date** — date picker, optional. Label: *"When was this last done?"*
     - **Odometer at last service** — numeric, optional. Label: *"Odometer at last service"*
     - Helper: *"If you remember when this was last done, enter it so we track from the right starting point. Not sure? Leave blank — we'll start from today."*

- **Save button**: "Add Item"
- Form max-width: ~520px

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: "Initial state" fields let users seed historical data when adding an item.
> **Why**: Without this, every newly added item would show "never performed" — potentially triggering false overdue warnings on day one for maintenance the user had done 2 months ago. This avoids the "everything is overdue the moment I set up the app" problem, which would violate the "no anxiety" principle.
> **Alternatives considered**: Always start from today (simpler but inaccurate), import history wizard (too complex for v1)
> **Confidence**: High

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: Warning thresholds are in a collapsed section with sensible defaults (500 mi / 14 days).
> **Why**: Most users will be fine with defaults. Showing thresholds prominently makes the form feel more complex than it is. The collapsed section says "this exists if you want it" without cluttering the happy path.
> **Alternatives considered**: Always visible, hidden entirely, per-item settings page
> **Confidence**: High

### Log Service Page

**What the user sees:**
- **Top bar**: ← back + "Log Service"
- **Context**: "Oil Change for Blue Thunder"
- **Form fields**:
  1. **Date** — date picker, defaults to today, no future dates
  2. **Odometer** — numeric, required. Helper: *"Last reading: 45,230 mi"*
  3. **Cost** — currency input, optional. Placeholder: *"Cost (optional)"*
  4. **Notes** — textarea, optional. Max 500 chars. Placeholder: *"Any notes? (optional)"*
- **Save button**: "Save Service Record"
- After save: interval clock resets from this date and odometer. Status recalculates.

### Edit Maintenance Item Page

- Same as Add Item, but pre-populated
- Title: "Edit Maintenance Item"
- Save button: "Save Changes"
- **Does NOT show the "Initial State" section** — that only appears at creation time. To change the baseline, the user logs a service record instead.
- **"Remove Item"** red text link at the bottom

### Edit Service Record Page

- Same as Log Service, but pre-populated
- Title: "Edit Service Record"
- Save button: "Save Changes"
- **"Delete this record"** red text link at the bottom

---

## Interaction Details

### Add Maintenance Item Flow
1. User navigates to vehicle detail → Maintenance tab
2. Taps "+ Add Item" → navigates to Add Item page
3. Fills in name (required) + at least one interval (mileage or time or both)
4. Optionally adjusts warning thresholds (expands collapsed section)
5. Optionally enters initial state (last performed date + odometer)
6. Taps "Add Item" → spinner → success → navigate back to maintenance tab
7. New item appears in correct urgency-sorted position
8. Toast: *"Oil Change added"*

### Log Service Flow
1. User taps an item to expand → taps "Log Service"
2. Navigates to Log Service page
3. Fills in date, odometer, optional cost + notes
4. Taps "Save Service Record" → spinner → success → navigate back
5. Item status recalculates. "Last performed" line updates. Record appears in history.
6. Toast: *"Service recorded!"*

### Remove Item Flow
1. User expands an item → taps "Remove Item"
2. **Confirmation dialog**: *"Remove 'Oil Change'? All service history for this item will also be removed."*
3. "Remove" (red) + "Cancel"
4. Confirm → item disappears from list. Toast: *"Oil Change removed"*

### Delete Service Record Flow
1. User expands an item → taps "⋯" on a record → "Delete"
2. **Confirmation**: *"Delete this service record from Jan 15, 2026?"*
3. Confirm → record removed. Status recalculates based on next most recent record.
4. Toast: *"Service record deleted"*

### Status Calculation Logic

**Inputs:**
- `lastServiceDate` and `lastServiceOdometer`: from the most recent service record (or initial state if no records exist)
- `estimatedCurrentMileage`: most recent odometer reading from any source (fuel log or maintenance record). Full linear interpolation comes in Sprint 05; for Sprint 04, use the raw most-recent reading.
- `currentDate`: today (NodaTime `LocalDate`)

**Mileage-based:**
- `milesSinceService` = `estimatedCurrentMileage` − `lastServiceOdometer`
- If `milesSinceService` ≥ `mileageInterval`: **Overdue**
- Else if `milesSinceService` ≥ `mileageInterval` − `mileageWarning`: **Due Soon**
- Else: **OK**

**Time-based:**
- `daysSinceService` = days between `lastServiceDate` and `currentDate`
- `intervalDays` = `timeIntervalMonths` × 30.44 (average days/month)
- If `daysSinceService` ≥ `intervalDays`: **Overdue**
- Else if `daysSinceService` ≥ `intervalDays` − `timeWarningDays`: **Due Soon**
- Else: **OK**

**Combined (both intervals set — "whichever comes first"):**
- Calculate both mileage-based and time-based status
- The **worse** status wins: if mileage = OK but time = Due Soon → item is Due Soon

**Never performed + no initial state:**
- Item shows as **Due Soon** (amber), NOT Overdue
- Status detail: *"No service history — log your first service to start tracking"*

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: Items with no history show as "Due Soon" (amber), never "Overdue" (red).
> **Why**: Showing everything as overdue on first setup creates alarm fatigue and violates the "no anxiety" principle. The user just created the item — they shouldn't feel behind already. "Due Soon" says "you should do this" without "you failed to do this."
> **Alternatives considered**: OK (hides that action is needed), Overdue (too aggressive), "Unknown" (new status adds complexity)
> **Confidence**: High

### Validation Rules

**Add/Edit Maintenance Item:**
| Field | Required | Validation | Error Message |
|-------|----------|------------|---------------|
| Name | Yes | 1–100 chars, non-blank after trim | *"Give this item a name"* |
| Mileage Interval | Conditional | Positive integer, max 999,999 | *"Enter a valid mileage interval"* |
| Time Interval | Conditional | Positive integer, 1–120 months | *"Enter a valid time interval (1–120 months)"* |
| At least one interval | Yes | Either mileage or time must be set | *"Set a mileage interval, time interval, or both"* |
| Mileage Warning | No | Positive integer, < mileage interval | *"Warning threshold must be less than the interval"* |
| Time Warning | No | Positive integer (days), < interval in days | *"Warning threshold must be less than the interval"* |
| Initial Date | No | Not in the future | *"Date can't be in the future"* |
| Initial Odometer | No | Positive integer, max 9,999,999 | *"Enter a valid odometer reading"* |

**Log/Edit Service Record:**
| Field | Required | Validation | Error Message |
|-------|----------|------------|---------------|
| Date | Yes | Not in the future | *"Date can't be in the future"* |
| Odometer | Yes | Positive integer, max 9,999,999 | *"Enter a valid odometer reading"* |
| Cost | No | Non-negative number, max 99,999.99 | *"Enter a valid cost"* |
| Notes | No | Max 500 chars | *"Notes are too long"* |

### Loading & Error States
- **Maintenance tab**: skeleton accordion cards (3 placeholders)
- **Form save failure**: inline banner above form
- **Delete failure**: error toast
- **Tab load failure**: inline error with "Retry"

---

## Edge Cases & Decisions

### Decision: Duplicate item names allowed?
**Decision**: Yes. A user might have "Tire Rotation — Front" and "Tire Rotation — Rear." No uniqueness constraint on names.
**Confidence**: High.

### Decision: What happens when intervals are edited?
**Decision**: Status immediately recalculates against the most recent service record using the new intervals. Reducing an interval could change status from OK to Due Soon or Overdue.
**Confidence**: High.

### Decision: Deleting the last service record
**Decision**: Item falls back to the next most recent record. If no records remain, it falls back to the initial state seeded at creation. If no initial state exists, it behaves as "never performed" (Due Soon). Initial state is stored as a property of the item, not as a service record.
**Confidence**: High.

### Decision: Time interval units
**Decision**: Months for the interval definition. Days for warning thresholds.
**Why**: Maintenance schedules are universally expressed in months ("every 3 months," "every 6 months"). Days would be confusing for intervals. But warning thresholds benefit from day-level granularity ("warn me 14 days before due" is more precise than "warn me 0.5 months before").
**Confidence**: High.

### Decision: Can maintenance cost be $0?
**Decision**: Yes. Some maintenance is DIY with no cost (e.g., checking tire pressure, topping off fluids from inventory).
**Confidence**: High.

### Edge case: Mileage interval set but no odometer readings exist anywhere
Status can't evaluate mileage-based status. Show time-based status only if set. If only mileage interval is set and no odometer exists: show "Due Soon" with detail *"Log a fill-up or service to start mileage tracking."*

### Edge case: Service record odometer lower than previous
Allow with warning (same pattern as fuel logs). Could be a correction or retroactive entry.

### Edge case: User sets unreasonably high warning threshold (e.g., 4,999 mile warning on 5,000 mile interval)
Validation catches this: warning must be less than interval. Error message guides the user.

---

## Out of Scope

- Pre-loaded maintenance schedules (PRD explicitly excludes)
- Push/email notifications for due items
- Cost tracking summaries or lifetime cost
- Dashboard integration with attention strip (Sprint 05)
- Attachment/receipt photos for service records
- Recurring service provider / shop tracking
- Maintenance item templates or sharing

---

## Dependencies

- **Sprint 02**: Vehicle CRUD, vehicle detail page with tab structure
- **Sprint 03**: Fuel logging (provides odometer readings for mileage estimation in status calculations)

---

## Data Contracts

### MaintenanceItem (read model — API → Frontend)
```
MaintenanceItem {
  itemId: string (UUID)
  vehicleId: string (UUID)
  name: string
  mileageInterval: number | null        // miles between services
  timeIntervalMonths: number | null     // months between services
  mileageWarning: number                // default 500
  timeWarningDays: number               // default 14
  status: "ok" | "dueSoon" | "overdue"
  statusDetail: string                  // human-readable detail
  lastPerformedDate: LocalDate | null
  lastPerformedOdometer: number | null
  records: MaintenanceRecord[]          // reverse chronological
  createdAt: Instant
}
```

### MaintenanceRecord
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
  items: MaintenanceItem[]              // sorted: overdue → dueSoon → ok → alphabetical within group
  lastOdometer: number | null           // most recent reading from any source
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
  mileageWarning: number                // 500 if not specified
  timeWarningDays: number               // 14 if not specified
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
