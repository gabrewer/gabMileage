import React from 'react'
import type { MeResponse } from '../api/me'

/**
 * Breadcrumbs (frontend-builder)
 * What: Minimal authenticated top bar with wordmark + user menu (Sign out).
 * Why: AC requires a global nav shell on authenticated pages; keep chrome light for mobile-first.
 * Alternatives considered: full sidebar nav (rejected; too heavy for Sprint 01), always-visible sign out button (rejected; user menu scales better).
 * Confidence: high
 */
export function TopBar({ me }: Readonly<{ me: MeResponse }>) {
  const [open, setOpen] = React.useState(false)

  return (
    <header
      data-testid="topbar"
      style={{
        borderBottom: '1px solid rgba(0,0,0,0.08)',
        background: 'white',
        position: 'sticky',
        top: 0,
        zIndex: 10,
      }}
    >
      <div
        style={{
          maxWidth: 1100,
          margin: '0 auto',
          padding: '12px 16px',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          gap: 12,
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <div
            aria-hidden
            style={{
              width: 28,
              height: 28,
              borderRadius: 8,
              background: 'linear-gradient(135deg, #fb7185, #f59e0b)',
            }}
          />
          <span style={{ fontWeight: 700, letterSpacing: -0.2 }}>gabMileage</span>
        </div>

        <div style={{ position: 'relative' }}>
          <button
            type="button"
            data-testid="user-menu-trigger"
            aria-haspopup="menu"
            aria-expanded={open}
            onClick={() => setOpen((v) => !v)}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 10,
              padding: '6px 10px',
              borderRadius: 12,
              border: '1px solid rgba(0,0,0,0.10)',
              background: 'white',
              cursor: 'pointer',
            }}
          >
            <Avatar name={me.displayName} avatarUrl={me.avatarUrl} />
            <span style={{ fontWeight: 600, fontSize: 14 }}>{me.displayName}</span>
            <span aria-hidden style={{ fontSize: 12, opacity: 0.7 }}>
              ▾
            </span>
          </button>

          {open ? (
            <div
              role="menu"
              aria-label="User menu"
              style={{
                position: 'absolute',
                right: 0,
                marginTop: 8,
                width: 220,
                borderRadius: 14,
                border: '1px solid rgba(0,0,0,0.10)',
                background: 'white',
                boxShadow: '0 8px 30px rgba(0,0,0,0.10)',
                overflow: 'hidden',
              }}
            >
              <div style={{ padding: 12 }}>
                <div style={{ fontWeight: 700, fontSize: 14 }}>{me.displayName}</div>
                <div style={{ fontSize: 12, opacity: 0.75 }}>{me.email}</div>
              </div>
              <div style={{ borderTop: '1px solid rgba(0,0,0,0.08)' }} />
              <form method="post" action="/api/auth/logout">
                <button
                  type="submit"
                  role="menuitem"
                  onClick={() => setOpen(false)}
                  style={{
                    width: '100%',
                    textAlign: 'left',
                    padding: 12,
                    border: 0,
                    background: 'white',
                    cursor: 'pointer',
                    fontWeight: 600,
                  }}
                >
                  Sign out
                </button>
              </form>
            </div>
          ) : null}
        </div>
      </div>
    </header>
  )
}

function Avatar({
  name,
  avatarUrl,
}: Readonly<{ name: string; avatarUrl: string | null }>) {
  if (avatarUrl) {
    return (
      <img
        src={avatarUrl}
        alt=""
        style={{ width: 28, height: 28, borderRadius: 999, objectFit: 'cover' }}
      />
    )
  }

  const initial = name.trim().slice(0, 1).toUpperCase() || 'U'
  return (
    <div
      aria-hidden
      style={{
        width: 28,
        height: 28,
        borderRadius: 999,
        background: 'rgba(251, 113, 133, 0.15)',
        color: '#be123c',
        display: 'grid',
        placeItems: 'center',
        fontWeight: 800,
        fontSize: 12,
      }}
    >
      {initial}
    </div>
  )
}
