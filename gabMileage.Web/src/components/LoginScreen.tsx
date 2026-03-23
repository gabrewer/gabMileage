import React from 'react'

/**
 * Breadcrumbs (frontend-builder)
 * What: Single-action login screen (one clear “Sign in” button).
 * Why: Fast entry, minimal cognitive load; WorkOS redirect flow is owned by the Gateway.
 * Alternatives considered: email field + magic link (rejected; out of scope; WorkOS hosted auth is the contract).
 * Confidence: high
 */
export function LoginScreen({ returnTo }: Readonly<{ returnTo: string }>) {
  return (
    <main
      data-testid="login-screen"
      style={{
        minHeight: '100vh',
        display: 'grid',
        placeItems: 'center',
        padding: 16,
        background:
          'radial-gradient(60% 60% at 20% 0%, rgba(251,113,133,0.14), rgba(255,255,255,0) 60%), radial-gradient(60% 60% at 100% 20%, rgba(245,158,11,0.14), rgba(255,255,255,0) 60%), white',
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
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <div
            aria-hidden
            style={{
              width: 36,
              height: 36,
              borderRadius: 12,
              background: 'linear-gradient(135deg, #fb7185, #f59e0b)',
            }}
          />
          <div>
            <div style={{ fontWeight: 900, fontSize: 18, letterSpacing: -0.2 }}>
              gabMileage
            </div>
            <div style={{ opacity: 0.75, fontSize: 13 }}>
              Keep your vehicles feeling cared for.
            </div>
          </div>
        </div>

        <div style={{ height: 14 }} />

        <p style={{ margin: 0, opacity: 0.8, fontSize: 14, lineHeight: 1.4 }}>
          Sign in to see your garage, log a fill-up, and stay gently ahead on
          maintenance.
        </p>

        <div style={{ height: 16 }} />

        <form method="get" action="/api/auth/login">
          <input type="hidden" name="returnTo" value={returnTo} />
          <button
            type="submit"
            style={{
              width: '100%',
              padding: '10px 12px',
              borderRadius: 14,
              border: 0,
              background: '#111827',
              color: 'white',
              fontWeight: 700,
              cursor: 'pointer',
            }}
          >
            Sign in
          </button>
        </form>
      </section>
    </main>
  )
}
