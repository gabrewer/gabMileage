# Sprint 05 — Dashboard & Attention System

> **Milestone 5 of 6** | Depends on: Sprint 04 (Maintenance Tracking)
> **Goal**: The dashboard comes alive with the "Needs Attention" strip — a consolidated view of overdue and due-soon maintenance across all vehicles. Garage cards gain badge counts. Linear interpolation provides smarter mileage estimation for status calculations.

---

## User Stories

### US-5.1: User sees what needs attention at a glance
**As a** user, **I want to** see a unified strip of all overdue and due-soon maintenance items across all my vehicles at the top of the dashboard **so that** I immediately know what needs attention without checking each vehicle individually.

### US-5.2: User sees attention badges on vehicle cards
**As a** user, **I want to** see a badge count on each vehicle card showing overdue + due-soon items **so that** I can visually identify which vehicles need attention.

### US-5.3: User navigates from attention item to action
**As a** user, **I want to** tap an attention item in the strip and land directly on the relevant vehicle's maintenance tab with that item expanded **so that** I can quickly log the service.

### US-5.4: System estimates mileage via linear interpolation
**As the** system, **I want to** estimate each vehicle's current mileage by extrapolating from the two most recent odometer readings **so that** maintenance status calculations stay reasonably accurate between fill-ups.

---

## Screen Descriptions

### Dashboard — Full Experience

**Layout (top to bottom):**

**Zone 1: "Needs Attention" Strip**
- Positioned below the top nav bar
- **Only visible when items need attention** — completely hidden when everything is OK
- **Background**: warm amber/light-yellow tint — not red (PRD principle: "no anxiety")
- **Heading**: "Needs Attention" (left-aligned, bold)
- **Attention cards** in a row:
  - Each card shows:
    - **Vehicle name** (small, secondary text above)
    - **Item name** (prominent, e.g., "Oil Change")
    - **Status badge**: red pill "Overdue" or amber pill "Due Soon"
    - **Status detail**: *"Overdue by ~800 mi"* or *"Due in ~3 days"*
  - Cards are tappable → deep-link to vehicle detail → maintenance tab → item expanded
- **Sort order**: Overdue first (most overdue → least overdue), then Due Soon (soonest → furthest)
- **Overflow at 6 items**: show first 6 + "See all (N)" link. Tapping "See all" expands the strip vertically to show all items; link changes to "Show less."

**Mobile-specific:**
- Attention cards in a **horizontal scroll** with scroll-snap (card width ~280px, 12px gap)
- Scroll is independent of page scroll
- No pagination dots — continuous snap scroll

**Desktop-specific:**
- Attention cards in a **wrapped grid** (2–3 per row, depending on width)
- If ≤6 items, all visible. If >6, "See all" expands vertically.

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: Attention strip uses warm amber background, not red. Individual "Overdue" badges within the strip are red.
> **Why**: A red strip at the top of every visit screams "ALARM." Amber says "here's something to be aware of" without panic. The "no anxiety" principle is paramount — the strip should be helpful, not stressful. Red badges on individual overdue items provide specificity without coloring the entire strip.
> **Alternatives considered**: Red background, white background with colored border, dismissible strip
> **Confidence**: High

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: Strip is completely hidden when everything is OK — no "All good!" message.
> **Why**: If everything is fine, the absence of the strip IS the signal. An "All good! 🎉" message becomes visual noise on every visit. Unnecessary positive reinforcement wastes space and attention. Users learn quickly: no strip = nothing to worry about.
> **Alternatives considered**: "All good!" banner, collapsed strip with green checkmark, always-visible strip with empty state
> **Confidence**: High

**Zone 2: Garage Card Grid (Enhanced)**
- Same card grid from Sprint 02, now with:
  - **Attention badge**: small circle in the top-right corner of each card
    - **Red badge** (number): if any items are overdue
    - **Amber badge** (number): if items are only due soon (none overdue)
    - **No badge**: if everything is OK
    - Badge shows combined count of overdue + due-soon items (e.g., "3")
  - All other card content unchanged (name, subtitle, photo/initial)

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: Badge color follows "worst status wins" — red if ANY item is overdue, amber if only due-soon items exist.
> **Why**: A single red badge clearly signals "something is past due." Mixing colors (e.g., "2 red + 1 amber") adds visual complexity with no practical benefit — the user's action is the same: tap to investigate.
> **Alternatives considered**: Split badges (red count + amber count), single-color badges only, priority icon instead of count
> **Confidence**: High

---

## Interaction Details

### Attention Strip → Vehicle Detail Deep Link
1. User taps an attention card in the strip
2. Navigates to the vehicle detail page → Maintenance tab (auto-selected)
3. The relevant maintenance item is **auto-expanded** (accordion opened)
4. If the item is below the fold, the page scrolls to bring it into view
5. User can immediately tap "Log Service" to record maintenance
6. After logging, navigating back to the dashboard refreshes the strip (item removed if no longer due)

### Dashboard Data Loading
1. App loads the dashboard (on navigation or initial page load)
2. **Skeleton state**: placeholder strip area (amber tint, no content) + skeleton card grid
3. API returns `DashboardData` with attention items + vehicle cards
4. **Render logic**:
   - If `attentionItems.length === 0` → hide strip entirely (don't flash an empty strip)
   - If `vehicles.length === 0` → show empty garage state (Sprint 01/02)
   - Otherwise → render strip + card grid
5. **Critical**: Data fetch is a single API call returning both zones. No waterfall of separate calls.

### Badge Count Refresh
- Badges update when the dashboard page loads (or is navigated to)
- **No real-time polling or WebSocket push in v1** — all state changes are user-initiated
- If a user logs a service on a vehicle and navigates back to the dashboard, the dashboard refetches data, and badges/strip update

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: No real-time updates — dashboard data refreshes on page load.
> **Why**: All changes in v1 are user-initiated (no external events, no shared garages, no push notifications). The user just performed the action and navigated back — a page-load refetch is perfectly fresh. Real-time adds infrastructure complexity for zero user benefit.
> **Alternatives considered**: WebSocket push, polling every 60s
> **Confidence**: High

### "See All" Expansion
- "See all (12)" is a toggle button — tapping expands the strip vertically to show all items in a single-column list
- Button text changes to "Show less"
- Tapping "Show less" collapses back to the 6-item view
- This is purely a client-side toggle — no navigation, no data fetch

---

## Mileage Estimation — Linear Interpolation

### Algorithm

The system estimates a vehicle's current mileage to feed maintenance status calculations without requiring the user to enter their odometer constantly.

**Inputs:**
- The two most recent odometer readings from any source (fuel logs or maintenance service records), each with a date: `(date1, odo1)` and `(date2, odo2)` where `date2 > date1`
- `today` — current date

**Calculation:**
1. `dailyRate = (odo2 - odo1) / daysBetween(date1, date2)`
2. `daysSinceLastReading = daysBetween(date2, today)`
3. `estimatedOdometer = odo2 + dailyRate × daysSinceLastReading`
4. **Cap**: `estimatedOdometer` ≤ `odo2 + (odo2 - odo1)` — never extrapolate more than one interval's worth of mileage. This prevents wildly inaccurate estimates for vehicles driven infrequently.

**Edge Cases:**
| Scenario | Behavior |
|----------|----------|
| Only 1 odometer reading | Use that reading as-is. No estimation. Status detail notes: *"Based on last reading of 45,230 mi on Mar 10"* |
| 0 odometer readings | Mileage-based status suppressed entirely. Only time-based status shown. |
| 2 readings on the same day | Daily rate = 0. Use `odo2` as-is (no extrapolation). |
| Very old readings (>6 months) | Still estimate with cap. Status detail appends: *"(estimate — log a fill-up for a better reading)"* |
| Daily rate is negative (odometer correction) | Clamp rate to 0. Use `odo2` as-is. |

**Display convention:**
- All estimated mileage values are prefixed with **~** (tilde) to indicate approximation
- Example: *"Next due in ~2,300 mi"* — the tilde tells the user it's an estimate
- Exact readings (from the reading itself, not extrapolated): no tilde

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: Tilde (~) prefix on all estimated mileage — never presented as exact.
> **Why**: Managing expectations is crucial. "45,832 mi" looks precise but is really a guess. "~45,800 mi" honestly signals it's approximate. This encourages users to log fill-ups for better accuracy and prevents false trust in the numbers.
> **Alternatives considered**: "(est.)" suffix, different text color, no indicator
> **Confidence**: High

### Where Estimation Is Used
- Maintenance status calculations (the `estimatedCurrentMileage` input in Sprint 04's logic)
- Status detail lines on maintenance items
- Dashboard attention strip details
- NOT on fuel log displays — those always show the actual recorded odometer

---

## Edge Cases & Decisions

### Decision: Vehicle has maintenance items but no fuel logs
**Decision**: Mileage estimation uses odometer readings from any source — including maintenance service records. The algorithm doesn't care whether the reading came from a fuel log or a service record. If there's only one reading total, no interpolation — just use the reading.
**Confidence**: High.

### Decision: "See all" threshold count
**Decision**: 6 items before "See all" appears. Fits ~2 rows of 3 on desktop. On mobile, represents ~2 screen-widths of horizontal scroll.
**Confidence**: Medium — may adjust based on card sizing in implementation.

### Decision: Archived vehicles in the attention strip
**Decision**: No. Archived vehicles (Sprint 06) are excluded from the dashboard, attention strip, and all badge calculations entirely.
**Confidence**: High.

### Decision: Badge color precedence
**Decision**: Red badge if ANY item is overdue. Amber badge only if all items are merely due-soon. Count combines both categories.
**Confidence**: High.

### Edge case: 0 vehicles
Dashboard shows empty garage state from Sprint 01/02. No strip. No cards.

### Edge case: Vehicles exist but no maintenance items defined
Strip hidden. Cards show no badges. This is expected — the user might only use fuel logging and never define maintenance items.

### Edge case: All items are OK
Strip hidden. No badges. Clean dashboard — just the garage grid. The absence of the strip IS the "all clear" signal.

### Edge case: User has 50+ attention items
"See all (50+)" link expands to a long scrollable list. No pagination — the list is finite and bounded by the user's vehicle and item count. Even power users are unlikely to have more than 20–30 items total.

---

## Out of Scope

- Push/email notifications for overdue items
- Dismissing or snoozing attention items
- Historical mileage graphs, charts, or trends
- Maintenance cost summaries or reporting
- Vehicle comparison dashboard
- Fuel economy trends on the dashboard
- Archived vehicles (Sprint 06)
- Real-time WebSocket updates

---

## Dependencies

- **Sprint 02**: Garage card grid, vehicle detail page
- **Sprint 03**: Fuel logging (provides odometer readings for interpolation)
- **Sprint 04**: Maintenance tracking (provides items, service records, and status logic)

---

## Data Contracts

### DashboardData (API → Frontend)
```
DashboardData {
  attentionItems: AttentionItem[]       // sorted by urgency
  vehicles: VehicleCard[]               // active only, sorted by createdAt desc
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
  statusDetail: string                  // e.g., "Overdue by ~800 mi"
  urgencyScore: number                  // higher = more urgent, for sort stability
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
  photoThumbnailUrl: string | null
  attentionCount: number                // overdue + due-soon combined
  hasOverdue: boolean                   // true if any overdue → red badge; false → amber badge
  createdAt: Instant
}
```

### EstimatedMileage (internal read model — used by status engine)
```
EstimatedMileage {
  vehicleId: string
  estimatedOdometer: number
  isEstimate: boolean                   // false if using exact reading (no interpolation)
  basedOnReadingDate: LocalDate
  confidence: "high" | "low"            // "low" if >6 months since last reading
}
```

### No New Domain Events
The dashboard and attention system are **read models** built from existing events (VehicleAdded, MaintenanceItemDefined, MaintenancePerformed, FuelLogRecorded, etc.). No new events are emitted.

### Marten Projections (New or Enhanced)
- **DashboardProjection**: aggregates vehicle, maintenance, and fuel events into `DashboardData`
- **EstimatedMileageProjection**: builds `EstimatedMileage` per vehicle from all odometer sources
- **MaintenanceStatusProjection** (enhanced): now uses `EstimatedMileage` instead of raw last reading for status calculations
