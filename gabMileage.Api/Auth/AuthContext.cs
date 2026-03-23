namespace gabMileage.Api.Auth;

// Breadcrumbs (backend-builder)
// What: Introduce AuthContext as a per-request DI value containing the authenticated userId.
// Why: Provides a consistent, testable way for endpoints/handlers to access user identity without parsing ClaimsPrincipal.
// Alternatives considered: Inject ClaimsPrincipal everywhere (rejected: inconsistent, mixes auth mechanism into handlers); static ambient context (rejected: hard to test).
// Confidence: high
public sealed record AuthContext(string UserId);
