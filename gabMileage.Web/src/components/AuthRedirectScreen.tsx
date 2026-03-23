import React from 'react'

/**
 * Breadcrumbs (frontend-builder)
 * What: Dedicated “Signing you in…” screen.
 * Why: Contract explicitly redirects to /auth/redirect after callback; also avoids UI flash while the session cookie settles.
 * Alternatives considered: redirect immediately to returnTo (rejected; harder to guarantee a stable UX).
 * Confidence: high
 */
export function AuthRedirectScreen() {
  return (
    <main
      data-testid="auth-redirect-screen"
      style={{
        minHeight: '100vh',
        display: 'grid',
        placeItems: 'center',
        padding: 16,
        background:
          'radial-gradient(60% 60% at 20% 0%, rgba(251,113,133,0.10), rgba(255,255,255,0) 60%), white',
      }}
    >
      <section
        style={{
          width: '100%',
          maxWidth: 420,
          borderRadius: 18,
          border: '1px solid rgba(0,0,0,0.08)',
          background: 'white',
          padding: 18,
          boxShadow: '0 10px 40px rgba(0,0,0,0.06)',
          display: 'grid',
          gap: 10,
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
          <Spinner />
          <div>
            <div style={{ fontWeight: 900, letterSpacing: -0.2 }}>
              Signing you in…
            </div>
            <div style={{ fontSize: 13, opacity: 0.75 }}>
              Almost there.
            </div>
          </div>
        </div>
      </section>

      <style>{`@keyframes spin{from{transform:rotate(0deg)}to{transform:rotate(360deg)}}`}</style>
    </main>
  )
}

function Spinner() {
  return (
    <div
      aria-hidden
      style={{
        width: 30,
        height: 30,
        borderRadius: 999,
        border: '3px solid rgba(17,24,39,0.15)',
        borderTopColor: '#111827',
        animation: 'spin 0.9s linear infinite',
      }}
    />
  )
}
