# Sprint 01 — Foundation & Auth

> **Milestone 1 of 6** | Depends on: nothing
> **Goal**: Stand up the full Aspire-orchestrated application skeleton with WorkOS authentication, so a user can log in and see an empty garage — the launchpad for everything that follows.

---

## User Stories

### US-1.1: Developer scaffolds the full solution
**As a** developer, **I want** the complete Aspire solution structure (AppHost, ServiceDefaults, API, Gateway, Web, test projects) wired and running locally **so that** all subsequent milestones build on a stable, observable foundation.

### US-1.2: User logs in via WorkOS
**As a** new or returning user, **I want to** authenticate via WorkOS (email + password, or social login) **so that** I have a secure, private garage tied to my identity.

### US-1.3: User sees an empty garage after first login
**As a** first-time user who just logged in, **I want to** land on a warm, inviting empty-state dashboard **so that** I feel welcomed and clearly see how to start — not confused by a blank page.

### US-1.4: User logs out
**As a** logged-in user, **I want to** sign out from any page **so that** my session is securely terminated.

### US-1.5: Returning user is automatically signed in
**As a** returning user with a valid session, **I want to** skip the login screen and land directly on my dashboard **so that** my experience is seamless.

---

## Screen Descriptions

### Login Screen

**What the user sees:**
- The gabMileage logo and tagline: *"Your garage journal"*
- A single "Sign in" button that initiates the WorkOS auth flow
- Minimalist layout — we don't render login fields ourselves; WorkOS owns the auth UI
- **Mobile**: Full-width centered content, generous vertical padding, logo + button stacked
- **Desktop**: Centered card (max-width ~420px) on a soft warm-gray background

**Actions available:**
- "Sign in" → redirects to WorkOS hosted authentication page

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: Single "Sign in" button — no form fields on our login page.
> **Why**: WorkOS handles the full auth UI (email/password, social). Duplicating form fields would be confusing and fragile. A single button keeps our login page dead simple.
> **Alternatives considered**: Embed WorkOS components inline, show email field with redirect
> **Confidence**: High

### Empty Garage (Dashboard — First Visit)

**What the user sees:**
- **Top bar**: gabMileage wordmark (left), user avatar circle + display name (right)
- **Center content** (vertically and horizontally centered):
  - A friendly garage-door illustration (line art, warm tones — not a photograph)
  - Heading: **"Your garage is empty"**
  - Subtext: *"Add your first vehicle to get started."*
  - Primary CTA: **"+ Add Vehicle"** button (large, prominent, warm accent color)
- No "Needs Attention" strip — nothing to show
- No garage card grid — no vehicles exist
- **Mobile**: Content stacks vertically, button is full-width, generous top margin
- **Desktop**: Centered column, max-width ~520px, vertically centered in viewport minus top bar

**Actions available:**
- "+ Add Vehicle" → navigates to the Add Vehicle page (Sprint 02 enables this; for now, it can be a placeholder route)
- User menu dropdown (tap avatar): displays "Sign out"

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: Empty state uses a warm illustration, not just text or a generic icon.
> **Why**: First impressions matter. A warm illustration ("garage door," "car outline in a cozy garage") sets the tone for the whole app — friendly, personal, journal-like. Text alone feels clinical.
> **Alternatives considered**: Just text + button, animated empty state, onboarding wizard
> **Confidence**: High

### Global Navigation Shell

**What the user sees:**
- **Mobile**: Simple top bar — logo/wordmark left, user avatar right. Height ~56px. No bottom tab bar.
- **Desktop**: Full-width top bar — logo left, user avatar + name right. Height ~64px.
- The shell wraps every authenticated page.
- **Back navigation**: On drill-down pages (vehicle detail, forms), a ← back arrow appears left of the page title in the top bar.

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: No bottom tab bar or sidebar — just a top bar with back-arrow drill-down.
> **Why**: The app has essentially two views: dashboard and vehicle detail. A bottom tab with one tab looks silly. A sidebar hides content for no benefit. The top bar is the lightest possible chrome — maximum content area.
> **Alternatives considered**: Bottom tab bar (Dashboard / Settings), hamburger sidebar
> **Confidence**: High — add navigation chrome when the app outgrows a single page hierarchy.

### User Menu Dropdown

**What the user sees on tap/click of the avatar:**
- Display name (non-interactive, just context)
- Email (smaller, gray text)
- Divider
- "Sign out" — only action in v1

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: The user menu is a dropdown from the avatar, not a separate settings page.
> **Why**: With only "Sign out" as an action, a full settings page is overkill. A dropdown is discoverable, fast, and doesn't disrupt navigation.
> **Alternatives considered**: Settings page, slide-out panel
> **Confidence**: High

---

## Interaction Details

### Authentication Flow (Detailed)
1. User visits the app → frontend checks for a valid auth cookie
2. **No cookie / expired**: render the Login screen
3. User taps "Sign in" → browser redirects to WorkOS hosted auth (`/api/auth/login` → 302 to WorkOS)
4. User authenticates on WorkOS (email/password or social)
5. WorkOS redirects back to our callback URL with an authorization code
6. YARP Gateway exchanges the code for a JWT, validates it, and sets an HTTP-only secure cookie
7. Gateway creates/updates a `UserProfile` read model in Marten (first-login provisioning)
8. User is redirected to the Dashboard (`/`)
9. **Returning user with valid cookie**: skip login, load dashboard directly

### Session Management
- **JWT in HTTP-only, secure, SameSite=Lax cookie** — not localStorage (XSS protection)
- Cookie name: `gab_session`
- Token lifetime: configured in WorkOS (recommend 7 days with sliding expiration)
- Token refresh: Gateway transparently refreshes before expiry on each request; if refresh fails, redirect to login
- "Sign out": `POST /api/auth/logout` → clears cookie → redirect to login page

### Loading States
- **Auth redirect**: Full-screen centered spinner with text *"Signing you in…"* — shown between WorkOS callback and dashboard load
- **Dashboard load**: Skeleton screen — a faint placeholder strip area + 3 skeleton cards where the garage grid would be. Transitions to either the empty state or the populated grid once data arrives
- **Critical**: Do NOT flash the empty state and then switch to the populated grid (or vice versa). Wait for data before deciding which state to render.

### Error States
- **WorkOS auth failure**: Friendly error page — *"Something went wrong signing you in. Please try again."* + "Try Again" button (back to login). No technical details.
- **Network error loading dashboard**: Inline error banner at the top of the content area — *"Couldn't load your garage. Check your connection and try again."* + "Retry" button. Do NOT show the empty state — empty state means "no data," not "couldn't load."
- **Session expired mid-use**: Any API 401 → redirect to login. Show a brief toast: *"Your session expired — please sign in again."*

---

## Edge Cases & Decisions

### Decision: Cookie-based JWT vs. localStorage
**Decision**: HTTP-only secure cookie.
**Why**: Prevents XSS from reading tokens. The YARP Gateway manages the cookie lifecycle. The frontend never touches the token directly.
**Confidence**: High.

### Decision: User registration
**Decision**: Registration is handled entirely by WorkOS as part of the auth flow. gabMileage has no sign-up page. First login = first time WorkOS has seen this user = registration.
**Why**: Fewer moving parts for v1. WorkOS managed user pool.
**Confidence**: High.

### Decision: User display name & profile
**Decision**: Pull `displayName` and `email` from the WorkOS user profile on first login. Store as a `UserProfile` read model. No user profile editing in v1.
**Confidence**: High.

### Decision: What if the same email registers via different providers?
**Decision**: WorkOS handles identity linking. We use the WorkOS user ID as our canonical identifier — if WorkOS considers them the same user, so do we.
**Confidence**: High.

### Edge case: User opens app in multiple tabs
Same session cookie — both tabs share state. No special handling. If one tab signs out, the other will get a 401 on next API call and redirect to login.

### Edge case: WorkOS is down during login
Show: *"We're having trouble signing you in right now. Please try again in a few minutes."* No automatic retry loop — user manually retries via the "Try Again" button.

### Edge case: User visits a deep-link while unauthenticated
Redirect to login, then after successful auth, redirect back to the originally requested URL (store the return URL in a query param or session).

**UX Breadcrumb:**
> **Who**: product-designer
> **What**: Deep-link return after login — the user gets back to where they were trying to go.
> **Why**: If a user bookmarks a vehicle detail page and visits it while signed out, losing their destination after login is frustrating. Preserving the return URL is standard practice and costs very little to implement.
> **Alternatives considered**: Always go to dashboard after login
> **Confidence**: High

---

## Out of Scope

- Adding vehicles (Sprint 02)
- Any data entry (fuel logs, maintenance)
- User profile editing (name, avatar, preferences)
- Settings page
- Password management / account deletion (WorkOS handles)
- Dashboard data (attention strip, badges, vehicle cards with data)
- Dark mode / theme selection

---

## Dependencies

None — this is the first milestone.

---

## Data Contracts

### UserProfile (read model — stored in Marten)
```
UserProfile {
  userId: string            // WorkOS user ID — primary identifier
  displayName: string       // from WorkOS profile
  email: string             // from WorkOS profile
  avatarUrl: string | null  // from WorkOS profile (social login may provide one)
  createdAt: Instant        // NodaTime — when the user first logged in to gabMileage
}
```

### AuthContext (available to all API requests via the Gateway)
```
AuthContext {
  userId: string            // extracted from JWT claims at the YARP Gateway
}
```

### GarageSummary (API → Frontend)
```
GarageSummary {
  vehicles: Vehicle[]       // empty array for new users
}
```
*(Vehicle shape defined in Sprint 02)*

### Domain Events
```
UserProvisioned {
  userId: string
  displayName: string
  email: string
  avatarUrl: string | null
  occurredAt: Instant
}
```

---

## Technical Notes

- Scaffold the full Aspire AppHost with all projects (API, Gateway, Web, ServiceDefaults)
- Configure MartenDB with PostgreSQL — initial event store schema, no projections yet
- Configure WolverineFx for command/event dispatch
- Set up YARP reverse proxy with JWT validation middleware and cookie management
- TanStack Start frontend: routing, auth context provider, nav shell component
- Redis configured in Aspire but not actively used yet (available for Sprint 05+)
- Health checks for all services via Aspire ServiceDefaults
- OpenTelemetry tracing configured via ServiceDefaults
- Playwright test scaffolding: login flow smoke test
- All NodaTime usage established — `Instant` for timestamps, `LocalDate` for user-facing dates
