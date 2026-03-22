# Sprint 01 — Foundation & Auth

> **Milestone 1 of 6** | Depends on: nothing
> **Goal**: Stand up the full application skeleton with authentication so a user can log in and see an empty garage.

---

## User Stories

### US-1.1: Developer scaffolds the solution
**As a** developer, **I want** the full Aspire-orchestrated solution structure in place **so that** all subsequent milestones build on a stable foundation.

### US-1.2: User logs in
**As a** new user, **I want to** log in via WorkOS **so that** I have a private, secure garage tied to my identity.

### US-1.3: User sees an empty garage
**As a** first-time user, **I want to** land on a warm, welcoming empty state after login **so that** I feel invited to start — not confused by a blank page.

### US-1.4: User logs out
**As a** logged-in user, **I want to** log out **so that** my session is securely ended.

---

## Screen Descriptions

### Login Screen

**What the user sees:**
- The gabMileage logo and tagline: *"Your garage journal"*
- A single "Sign in" button that triggers the WorkOS auth flow
- Clean, minimal — no form fields on our side (WorkOS handles the auth UI)
- Mobile: full-width centered content, generous padding
- Desktop: centered card on a soft background

**Actions available:**
- Sign in (redirects to WorkOS hosted auth page)

### Empty Garage (Dashboard — first visit)

**What the user sees:**
- Top: gabMileage logo/wordmark (left), user avatar + name dropdown (right)
- Center: A friendly illustration or icon of a garage with the message:
  - Heading: **"Your garage is empty"**
  - Subtext: *"Add your first vehicle to get started."*
  - Primary CTA button: **"+ Add Vehicle"** (large, prominent)
- No "Needs Attention" strip (nothing to show yet)
- No garage grid (no vehicles yet)
- Mobile: stacked vertically, button is full-width
- Desktop: centered content with max-width of ~600px

**Actions available:**
- "Add Vehicle" button (navigates to Add Vehicle — Sprint 02)
- User menu dropdown: "Sign out"

### Global Navigation Shell

**What the user sees:**
- **Mobile**: Simple top bar with logo (left) and user avatar (right). No bottom tab bar for v1 — there's only one main page (the dashboard). Vehicle detail is a drill-down.
- **Desktop**: Top bar spanning full width. Logo left, user menu right.
- The nav shell wraps all authenticated pages.

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: No bottom tab bar or sidebar navigation — just a top bar.
> **Why**: The PRD describes essentially two views (dashboard and vehicle detail). A bottom tab bar with only one or two tabs feels wasteful. A simple top bar with back-navigation for drill-downs is cleaner.
> **Alternatives considered**: Bottom tab bar (Dashboard / Settings), sidebar nav
> **Confidence**: High — if we add more top-level sections later, we can introduce a bottom bar then.

---

## Interaction Details

### Authentication Flow
1. User visits the app → lands on the Login screen (if not authenticated)
2. User clicks "Sign in" → redirected to WorkOS hosted auth
3. WorkOS handles login/registration → redirects back with auth code
4. YARP Gateway validates the JWT from WorkOS and establishes the session
5. User lands on the Dashboard (empty garage for first-time users)
6. On subsequent visits, if the JWT is still valid, the user skips login

### Session Management
- JWT stored in an HTTP-only secure cookie (not localStorage — XSS protection)
- Token refresh handled automatically by the gateway
- If the token expires and can't be refreshed, the user is redirected to login
- "Sign out" clears the cookie and redirects to login

### Loading States
- Login redirect: show a full-screen spinner with "Signing you in…" text
- Dashboard load: show a skeleton screen (placeholder rectangles) while the garage data loads. For empty garages, transition to the empty state illustration.

### Error States
- WorkOS auth failure: show a friendly error page with "Something went wrong signing you in. Please try again." and a retry button.
- Network error on dashboard load: show inline error message with retry. Don't flash the empty state — distinguish between "no data" and "failed to load."

---

## Edge Cases & Decisions

### Decision: Cookie-based JWT vs. localStorage
**Decision**: HTTP-only secure cookies.
**Why**: Prevents XSS from accessing tokens. The gateway sets and manages the cookie.
**Confidence**: High.

### Decision: User registration
**Decision**: WorkOS handles registration as part of the login flow. We don't have a separate sign-up page.
**Why**: WorkOS managed user pool — fewer moving parts for v1.
**Confidence**: High.

### Decision: User display name
**Decision**: Pull the user's name from WorkOS user profile on first login. Store a `UserProfile` read model with `userId`, `displayName`, and `email`. The user cannot edit their profile in v1 — it comes from WorkOS.
**Confidence**: High.

### Edge case: User opens app in multiple tabs
Same session cookie — both tabs share the session. No special handling needed.

### Edge case: WorkOS is down
Show a generic error screen: "We're having trouble signing you in right now. Please try again in a few minutes." No retry loop — user manually retries.

---

## Out of Scope

- Adding vehicles (Sprint 02)
- Any data entry (fuel logs, maintenance)
- User profile editing
- Settings page
- Password management (WorkOS handles this)
- Any dashboard data (attention strip, badges)

---

## Dependencies

None — this is the first milestone.

---

## Data Contracts

### User Profile (read model)
```
UserProfile {
  userId: string          // WorkOS user ID
  displayName: string     // from WorkOS profile
  email: string           // from WorkOS profile
  createdAt: Instant      // NodaTime — when the user first logged in
}
```

### Auth Context (available to all API requests)
```
AuthContext {
  userId: string          // extracted from JWT at the gateway
}
```

### Garage Summary (API → Frontend)
```
GarageSummary {
  vehicles: Vehicle[]     // empty array for new users
}
```
(Vehicle shape defined in Sprint 02)

---

## Technical Notes

- Set up the full Aspire AppHost with all projects (API, Gateway, Web, ServiceDefaults)
- Configure MartenDB with PostgreSQL and initial event store schema
- Configure WolverineFx for command/event dispatch
- Set up YARP reverse proxy with JWT validation middleware
- TanStack Start frontend with routing, auth context provider, and the nav shell
- Redis configured but not actively used yet
- Health checks for all services via Aspire ServiceDefaults
- OpenTelemetry tracing configured via ServiceDefaults
