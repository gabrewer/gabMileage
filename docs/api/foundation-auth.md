# gabMileage API Contract — Sprint 01: foundation-auth

> **Sprint**: foundation-auth  
> **Owner**: api-developer  
> **Last updated**: 2026-03-22  
> **Base**: `/api/v1/` for application data; `/api/auth/*` for interactive auth redirects

This document defines the HTTP contract between **gabMileage.Web** (TanStack Start) and the **gabMileage backend** (Gateway + Api) for Sprint 01.

---

## 0) Conventions

### 0.1 Authentication model (browser-facing)
- Browser holds an **HTTP-only** session cookie named **`gab_session`** containing a WorkOS JWT.
- The **Gateway** owns:
  - WorkOS redirect flow
  - JWT validation
  - refresh-before-expiry
  - setting/clearing the cookie
- The frontend **never** reads tokens (no localStorage).

### 0.2 Authentication model (Gateway → Api)
For requests proxied from Gateway to Api, the Gateway forwards the authenticated identity as:

- `X-User-Id: {workosUserId}` where `{workosUserId}` is the JWT `sub` claim.

The Api derives `AuthContext.UserId` from this header.

> Note: This is an **internal** contract (Gateway ↔ Api). Frontend callers do not set `X-User-Id`.

### 0.3 Content types
- Requests/responses use `application/json` unless otherwise noted.
- Error responses use `application/problem+json` (RFC 7807 ProblemDetails).

### 0.4 Time and date serialization
- All timestamps are ISO 8601 UTC strings (NodaTime `Instant`): e.g. `"2026-03-22T23:26:39Z"`.
- All dates are ISO 8601 date strings (NodaTime `LocalDate`): e.g. `"2026-03-22"`.

### 0.5 Common errors (ProblemDetails)
The backend returns ProblemDetails bodies for validation/business errors.

**Example (401):**
```json
{
  "type": "https://httpstatuses.com/401",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Authentication required.",
  "traceId": "00-..."
}
```

**Example (400 validation):**
```json
{
  "type": "https://httpstatuses.com/400",
  "title": "Bad Request",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "errors": {
    "returnTo": ["Must be a relative path (no scheme/host)."]
  },
  "traceId": "00-..."
}
```

---

## 1) Health endpoints (dev-only)

These endpoints exist for Aspire orchestration and are typically **only mapped in Development**.

### GET /health
Readiness health check.

**Auth**: public (development only)  
**Response 200 OK**: framework-defined health payload (not consumed by the frontend).

### GET /alive
Liveness health check.

**Auth**: public (development only)  
**Response 200 OK**: framework-defined health payload (not consumed by the frontend).

---

## 2) Gateway auth endpoints (interactive redirects)

These endpoints are implemented by **gabMileage.Gateway**.

### GET /api/auth/login
Initiate WorkOS hosted auth.

**Auth**: public  
**Owning vertical slice**: `Gateway.Auth`

**Query params:**
- `returnTo` (optional): string; a **relative** path the user should be returned to after login (e.g. `/`, `/vehicles/abc`).

**Behavior:**
- Validates `returnTo` is a safe relative URL (no scheme/host, no `//`, no backslashes).
- Responds with `302 Found` to the WorkOS authorization URL.

**Response 302 Found**
- `Location: https://api.workos.com/...` (WorkOS hosted auth)
- May set a short-lived correlation cookie for state/nonce (implementation detail)

**Errors:**
- `400 Bad Request` (ProblemDetails): invalid `returnTo` value

---

### GET /api/auth/callback
WorkOS redirect callback.

**Auth**: public (called by WorkOS)  
**Owning vertical slice**: `Gateway.Auth`

**Query params (from WorkOS):**
- `code` (required): authorization code
- `state` (optional/required by WorkOS config): opaque value used to prevent CSRF and to carry the intended return URL

**Behavior (success):**
1. Exchange `code` for a WorkOS JWT.
2. Validate JWT.
3. Set `gab_session` cookie:
   - `HttpOnly; Secure; SameSite=Lax; Path=/`
4. Provision the user (idempotent): Gateway sends `ProvisionUser` to Api.
5. Redirect the browser to an application route that shows the "Signing you in…" loading screen.

**Response 302 Found (success)**
- `Set-Cookie: gab_session=...; HttpOnly; Secure; SameSite=Lax; Path=/`
- `Location: /auth/redirect?returnTo={returnTo}`

> `returnTo` is whatever was supplied to `/api/auth/login`, subject to validation and any state correlation.

**Behavior (failure):**
- If code exchange fails, token invalid, or provisioning fails:
  - clear any existing `gab_session` cookie (best-effort)
  - redirect to a friendly error page

**Response 302 Found (failure)**
- `Set-Cookie: gab_session=; Expires=Thu, 01 Jan 1970 00:00:00 GMT; Path=/`
- `Location: /auth/error?code=auth_failed`

**Errors (non-redirect callers):**
If the callback is invoked in a non-browser context, the Gateway may return:
- `400 Bad Request` (ProblemDetails): missing `code`
- `401 Unauthorized` (ProblemDetails): invalid/expired code or token

---

### POST /api/auth/logout
Terminate the session.

**Auth**: requires session cookie (but should succeed even if already logged out)  
**Owning vertical slice**: `Gateway.Auth`

**Request body:** none

**Behavior:**
- Clears `gab_session` cookie.
- Redirects the browser to the login page.

**Response 303 See Other**
- `Set-Cookie: gab_session=; Expires=Thu, 01 Jan 1970 00:00:00 GMT; Path=/`
- `Location: /login`

**Errors:**
- None expected. This endpoint should be idempotent.

---

## 3) Api endpoints (application data)

These endpoints are implemented by **gabMileage.Api** and are normally called by the frontend via the Gateway reverse proxy.

### Auth requirements for `/api/v1/*`
- All `/api/v1/*` endpoints require authentication.
- If unauthenticated/expired:
  - Gateway should return `401 Unauthorized` (ProblemDetails) for XHR/fetch.
  - Frontend should treat any 401 as a signal to route to `/login`.

---

### GET /api/v1/me
Get the current authenticated user’s profile for rendering the navigation shell.

**Auth**: required  
**Owning vertical slice**: `Api.Users`

**Response 200 OK**
```json
{
  "userId": "string",
  "displayName": "string",
  "email": "string",
  "avatarUrl": "string | null",
  "createdAt": "2026-03-22T23:26:39Z"
}
```

**Errors:**
- `401 Unauthorized` (ProblemDetails): missing/invalid session
- `404 Not Found` (ProblemDetails): user profile not found for this user (should be rare; indicates provisioning didn’t happen)

---

### GET /api/v1/garage/summary
Fetch the user’s garage summary for the dashboard.

**Auth**: required  
**Owning vertical slice**: `Api.Garage`

**Response 200 OK (Sprint 01)**
```json
{
  "userId": "string",
  "vehicles": []
}
```

Notes:
- In Sprint 01, `vehicles` is always an empty array.
- In Sprint 02, `vehicles[]` will be expanded to objects (extend-only evolution).

**Errors:**
- `401 Unauthorized` (ProblemDetails): missing/invalid session

---

## 4) Wolverine messages (internal)

These messages are part of the internal contract between the **Gateway** (caller) and **Api** (handler). They are not called by browsers directly, but they are essential to the end-to-end auth experience.

### Command: ProvisionUser
Idempotently provision a gabMileage `User` upon successful authentication.

**Owning vertical slice**: `Api.Users`

**C# record (domain model):**
```csharp
public sealed record ProvisionUser(
    string UserId,
    string DisplayName,
    string Email,
    string? AvatarUrl
);
```

**Validation rules:**
- `UserId` required, non-empty (WorkOS `sub`)
- `DisplayName` required, non-empty
  - If WorkOS returns an empty display name, Gateway (or handler) must fall back to the email local-part.
- `Email` required, non-empty

**Idempotency:**
- If the user is already provisioned, the handler is a no-op and emits no events.

---

### Event: UserProvisioned
Emitted once, the first time a WorkOS identity is seen by gabMileage.

**Owning vertical slice**: `Api.Users`

**C# record (domain model):**
```csharp
public sealed record UserProvisioned(
    string UserId,
    string DisplayName,
    string Email,
    string? AvatarUrl,
    Instant OccurredAt
);
```

---

## 5) Server-sent events (SSE)

No SSE streams are introduced in Sprint 01.

---

## 6) Breadcrumbs (contract decisions)

### D-API-01: Auth endpoints are `/api/auth/*` (non-versioned)
- **Who**: api-developer
- **What**: Keep auth redirect endpoints at `/api/auth/login`, `/api/auth/callback`, `/api/auth/logout` (not under `/api/v1`).
- **Why**: These are interactive redirect flows (not resource CRUD) and are explicitly named in the sprint brief. Versioning the redirect surface adds churn without benefit.
- **Alternatives considered**:
  - `/api/v1/auth/*` → rejected: unnecessary coupling to resource API versioning.
- **Confidence**: High

### D-API-02: Provide `GET /api/v1/me` for nav shell
- **Who**: api-developer
- **What**: Add a dedicated endpoint returning the persisted `UserProfile` fields needed by the UI shell.
- **Why**: Display name/avatar are sourced from WorkOS at provisioning time and stored in Marten. The frontend needs a stable, app-owned way to render the shell without parsing JWT claims.
- **Alternatives considered**:
  - Derive name/avatar from JWT claims in the frontend → rejected: token is HttpOnly; also profile may evolve independently.
  - Embed user profile in HTML bootstrap → rejected: TanStack Start already fetches data; keep a consistent JSON contract.
- **Confidence**: High

### D-API-03: `GarageSummary` includes `userId` and an extend-only `vehicles` array
- **Who**: api-developer
- **What**: Return `{ userId, vehicles: [] }` for Sprint 01.
- **Why**: `userId` helps with tracing/support and matches the domain read model. `vehicles` is present now to prevent UI contract churn when vehicle objects are introduced in Sprint 02.
- **Alternatives considered**:
  - Return only `{ vehicles: [] }` → rejected: less diagnosable; adding `userId` later is safe but including now is fine.
- **Confidence**: Medium

### D-API-04: Callback redirects to `/auth/redirect` to support "Signing you in…" UI
- **Who**: api-developer
- **What**: On successful callback, redirect to `/auth/redirect?returnTo=...` (frontend route).
- **Why**: Product requires an explicit interim loading screen between WorkOS callback and dashboard rendering. A dedicated route makes it easy to show a stable, testable UX.
- **Alternatives considered**:
  - Redirect directly to `returnTo` → rejected: harder to guarantee the loading screen and consistent "no flash" behavior.
- **Confidence**: High
