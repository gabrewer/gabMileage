# Sprint 05 — Dashboard & Attention System

> **Milestone 5 of 6** | Depends on: Sprint 04 (Maintenance Tracking)
> **Goal**: The dashboard comes alive with a "Needs Attention" strip showing overdue and due-soon items across all vehicles, and garage cards display badge counts. Mileage estimation via linear interpolation provides accurate status calculations.

---

## User Stories

### US-5.1: User sees what needs attention at a glance
**As a** user, **I want to** see a consolidated strip of all overdue and due-soon maintenance items across all my vehicles at the top of my dashboard **so that** I immediately know what needs attention without drilling into each vehicle.

### US-5.2: User sees attention badges on vehicle cards
**As a** user, **I want to** see a badge count on each vehicle card showing how many items need attention **so that** I can quickly identify which vehicles have pending maintenance.

### US-5.3: User taps an attention item to take action
**As a** user, **I want to** tap on an attention item and go directly to the relevant vehicle's maintenance tab **so that** I can quickly log the service.

### US-5.4: System estimates current mileage
**As a** system, **I want to** estimate each vehicle's current mileage using linear interpolation between odometer readings **so that** maintenance due-date calculations are more accurate than waiting for the next fuel log.

---

## Screen Descriptions

### Dashboard — Full Experience

**What the user sees:**

**Zone 1: "Needs Attention" Strip**
- Positioned at the top of the dashboard, below the nav bar
- **Only visible if there are items needing attention** — hidden entirely when everything is OK
- Background: a warm amber/light-yellow tint (not red — "no anxiety" principle)
- Heading: **"Needs Attention"** (left-aligned)
- **Item cards** in a horizontal scrollable row (mobile) or wrapped grid (desktop):
  - Each card shows:
    - **Vehicle name** (small, above the item name)
    - **Maintenance item name** (prominent, e.g., "Oil Change")
    - **Status badge**: red "Overdue" or amber "Due Soon"
    - **Status detail**: *"Overdue by ~800 mi"* or *"Due in ~3 days"*
  - Cards are tappable → navigate to that vehicle's maintenance tab with the item expanded
- **Sort order**: Overdue items first (sorted by how overdue — worst first), then Due Soon items (sorted by how close — nearest first)
- If more than 6 items: show first 6 + a "See all (12)" link. The "See all" expands to show all items vertically.

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: The attention strip uses a warm amber background, not red. Overdue badges within the strip are red, but the strip container itself is amber.
> **Why**: PRD principle: "No anxiety — reminders should feel helpful, not nagging." A red strip at the top of every page visit screams alarm. Amber says "here's something to be aware of" without panic. The individual overdue badges within the strip provide specificity.
> **Alternatives considered**: Red strip, no colored background (just a list), dismissible strip
> **Confidence**: High

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: The attention strip is hidden when everything is OK, not shown with an "All good!" message.
> **Why**: If everything is fine, the user doesn't need to be told that everything is fine. An "All good!" message takes up space and becomes visual noise on every visit. The absence of the strip IS the signal that everything is OK.
> **Alternatives considered**: "All good! 🎉" message, minimized strip with green checkmark
> **Confidence**: High

**Zone 2: Garage Card Grid (Enhanced)**
- Same as Sprint 02, but each card now has:
  - **Attention badge**: a small circle in the top-right corner of the card showing the count of overdue + due-soon items
    - Red badge if any items are overdue
    - Amber badge if items are only due soon (none overdue)
    - No badge if everything is OK
  - Badge shows the count number (e.g., "2")
- Everything else about the card grid remains the same

**Mobile layout:**
- Attention strip: horizontal scroll of cards, each card ~280px wide, 16px gap
- Garage grid: single column below the strip
- Total layout: attention strip scrolls independently of the page scroll

**Desktop layout:**
- Attention strip: wrapped grid of cards, 2-3 per row, within a max-width container
- Garage grid: 2-3 columns below
- Both in the same scroll context

---

## Interaction Details

### Attention Strip Navigation
1. User sees the attention strip with items
2. Taps an attention card
3. Navigates to the vehicle detail page → Maintenance tab
4. The relevant maintenance item is auto-expanded (scrolled into view if needed)
5. From there, the user can tap "Log Service" to record maintenance

### Dashboard Data Loading
1. App loads the dashboard
2. Show skeleton for both zones: a placeholder strip + skeleton cards
3. API returns:
   - List of attention items (overdue + due soon, across all vehicles)
   - List of vehicles with badge counts
4. If no attention items → hide the strip entirely (no flash of empty strip)
5. If no vehicles → show empty garage state (Sprint 01/02)

### Attention Strip Interaction Details
- **Mobile**: horizontal scroll. No pagination dots — continuous scroll. Cards snap to position (scroll-snap).
- **Desktop**: if 6 or fewer items, show them in a wrapped grid. If more, show 6 + "See all (N)" link.
- "See all" click: the strip expands vertically to show all items in a 1-column list. Button changes to "Show less". This is a toggle — no page navigation.

### Badge Count Updates
- Badges should update when the user logs a service (navigates back to dashboard after Sprint 04 actions)
- No real-time polling in v1 — the dashboard refreshes on navigation (client-side refetch on page load)

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: No real-time badge updates or WebSocket push. Dashboard data refreshes on page load.
> **Why**: For v1, the user manually triggers all changes. There are no external events that would change badge counts between page loads. Real-time updates add complexity with no user benefit until we have push notifications or shared garages.
> **Alternatives considered**: WebSocket for live updates, periodic polling
> **Confidence**: High

---

## Mileage Estimation (Linear Interpolation)

### Algorithm

The system estimates current mileage to determine maintenance status without requiring the user to enter their odometer for every status check.

**Inputs**:
- Two most recent odometer readings (from any source: fuel logs or maintenance records), each with a date
- Current date

**Calculation**:
1. Take the two most recent chronological odometer readings: `(date1, odo1)` and `(date2, odo2)` where `date2 > date1`
2. Calculate daily mileage rate: `rate = (odo2 - odo1) / daysBetween(date1, date2)`
3. Estimate current mileage: `estimatedOdo = odo2 + rate × daysSince(date2)`
4. Cap the estimate at a maximum of `odo2 + (odo2 - odo1)` — don't extrapolate more than one interval's worth of mileage to avoid wildly inaccurate estimates for vehicles driven infrequently.

**Edge cases:**
- **Only one odometer reading**: can't estimate. Use the single reading as-is. Status calculations that depend on mileage show the last known reading and note: *"Based on last reading of 45,230 mi on Mar 10"*
- **No odometer readings**: can't estimate at all. Mileage-based status is suppressed; only time-based status is shown.
- **Two readings on the same day**: daily rate is 0. Use `odo2` as-is (no extrapolation).
- **Very old readings** (> 6 months since last reading): still use the estimation but cap is very important here. The status detail line should include *"(estimate — log a fill-up for a better reading)"* to encourage the user to update.

**Where estimation is used:**
- Maintenance status calculations (Sprint 04 logic uses estimated current mileage)
- Status detail lines (showing "~2,300 mi" with the tilde indicating it's an estimate)
- Dashboard attention strip details

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: Estimated mileage is always indicated with a tilde (~) prefix and never presented as exact.
> **Why**: Users should understand that the number is approximate. This manages expectations and encourages them to log fill-ups for more accurate tracking. It also avoids false precision — showing "45,832 mi" when it's really an estimate would be misleading.
> **Alternatives considered**: No indicator (treat estimates as facts), "(estimated)" label, different color
> **Confidence**: High

---

## Edge Cases & Decisions

### Decision: What if a vehicle has maintenance items but no fuel logs?
**Decision**: Maintenance status calculations can only use odometer readings from maintenance service records. The mileage estimation algorithm still works — it just uses service records instead of fuel logs. If there's only one reading, show the last known value without estimation.
**Confidence**: High.

### Decision: How many items in the attention strip before "See all"?
**Decision**: 6 items. This fits nicely on desktop (2 rows of 3) and represents about 2 screen-widths of scrolling on mobile (3 cards per screen-width × 2).
**Confidence**: Medium — may need adjustment based on card sizing.

### Decision: Does the attention strip count archived vehicles?
**Decision**: No. Archived vehicles (Sprint 06) are completely removed from the dashboard, attention strip, and all badge calculations.
**Confidence**: High.

### Decision: Badge color precedence
**Decision**: If a vehicle has both overdue and due-soon items, the badge is red (worst status wins). The count includes both overdue and due-soon items combined.
**Confidence**: High.

### Edge case: User has 0 vehicles
Dashboard shows the empty garage state from Sprint 01/02. No attention strip. No card grid.

### Edge case: User has vehicles but no maintenance items defined
Attention strip is hidden (nothing to track). Vehicle cards show no badges. This is fine — the user might only use fuel logging.

### Edge case: All items are OK
Attention strip is hidden. Vehicle cards show no badges. The dashboard is clean — just the garage grid.

---

## Out of Scope

- Push/email notifications for overdue items
- Dismissing individual attention items
- Snoozing due-soon items
- Historical mileage graphs or charts
- Maintenance cost summaries
- Vehicle comparison views
- Archived vehicles (Sprint 06)

---

## Dependencies

- Sprint 02: Garage card grid, vehicle detail view
- Sprint 03: Fuel logging (provides odometer readings for estimation)
- Sprint 04: Maintenance tracking (provides the items and status logic)

---

## Data Contracts

### DashboardData (API → Frontend)
```
DashboardData {
  attentionItems: AttentionItem[]      // sorted by urgency
  vehicles: VehicleCard[]              // sorted by createdAt desc
}
```

### AttentionItem
```
AttentionItem {
  vehicleId: string (UUID)
  vehicleName: string
  itemId: string (UUID)
  itemName: string
  status: "dueSoon" | "overdue"
  statusDetail: string                 // human-readable, e.g., "Overdue by ~800 mi"
  urgencyScore: number                 // for sorting: higher = more urgent
}
```

### VehicleCard (enhanced from Sprint 02)
```
VehicleCard {
  vehicleId: string (UUID)
  name: string
  make: string | null
  model: string | null
  year: number | null
  photoUrl: string | null
  attentionCount: number              // overdue + due-soon items
  hasOverdue: boolean                 // true if any item is overdue (for red vs amber badge)
  createdAt: Instant
}
```

### EstimatedMileage (internal, used by status calculations)
```
EstimatedMileage {
  vehicleId: string
  estimatedOdometer: number
  isEstimate: boolean                 // false if using exact last reading
  basedOnReadingDate: LocalDate       // date of most recent reading used
  confidence: "high" | "low"          // "low" if > 6 months since last reading
}
```

### Domain Events

No new domain events in this sprint — the dashboard and attention system are read models built from existing events (VehicleAdded, MaintenanceItemDefined, MaintenancePerformed, FuelLogRecorded, etc.).

### Marten Projections

This sprint introduces or extends these Marten projections:
- **DashboardProjection**: builds `DashboardData` read model from vehicle, maintenance, and fuel events
- **EstimatedMileageProjection**: builds `EstimatedMileage` per vehicle from odometer readings across all sources
- **MaintenanceStatusProjection**: enhanced to use estimated mileage for status calculations
