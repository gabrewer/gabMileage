import React from 'react'

/**
 * Breadcrumbs (frontend-builder)
 * What: Warm empty garage state with illustration + clear “+ Add Vehicle” CTA.
 * Why: Prevents the dashboard from feeling blank; encourages first action in <30 seconds.
 * Alternatives considered: plain text empty state (rejected; too cold), immediately prompting a full vehicle form (rejected; Sprint 02).
 * Confidence: high
 */
export function DashboardEmptyState({
  onAddVehicle,
}: Readonly<{ onAddVehicle: () => void }>) {
  return (
    <section
      data-testid="empty-garage-state"
      style={{
        borderRadius: 18,
        border: '1px solid rgba(0,0,0,0.08)',
        background: 'white',
        padding: 18,
        display: 'grid',
        gap: 14,
        justifyItems: 'start',
      }}
    >
      <Illustration />
      <div>
        <h1 style={{ margin: 0, fontSize: 20, letterSpacing: -0.2 }}>
          Your garage is ready.
        </h1>
        <p style={{ margin: '6px 0 0', opacity: 0.78, lineHeight: 1.4 }}>
          Add your first vehicle and we’ll help you keep fuel logs and
          maintenance feeling calm—not complicated.
        </p>
      </div>

      <button
        type="button"
        onClick={onAddVehicle}
        style={{
          padding: '10px 12px',
          borderRadius: 14,
          border: 0,
          background: '#111827',
          color: 'white',
          fontWeight: 800,
          cursor: 'pointer',
        }}
      >
        + Add Vehicle
      </button>
    </section>
  )
}

function Illustration() {
  return (
    <svg
      width="240"
      height="120"
      viewBox="0 0 240 120"
      role="img"
      aria-label="A cozy empty garage"
      style={{ borderRadius: 16, background: 'rgba(251,113,133,0.06)' }}
    >
      <defs>
        <linearGradient id="g" x1="0" x2="1" y1="0" y2="1">
          <stop offset="0" stopColor="#fb7185" stopOpacity="0.55" />
          <stop offset="1" stopColor="#f59e0b" stopOpacity="0.55" />
        </linearGradient>
      </defs>
      <rect x="12" y="18" width="216" height="84" rx="18" fill="url(#g)" />
      <rect x="26" y="34" width="188" height="54" rx="14" fill="white" fillOpacity="0.9" />
      <path
        d="M70 78c6-12 18-20 32-20h34c14 0 26 8 32 20"
        fill="none"
        stroke="#111827"
        strokeOpacity="0.25"
        strokeWidth="6"
        strokeLinecap="round"
      />
      <circle cx="92" cy="80" r="10" fill="#111827" fillOpacity="0.22" />
      <circle cx="168" cy="80" r="10" fill="#111827" fillOpacity="0.22" />
      <path
        d="M104 62h32"
        stroke="#111827"
        strokeOpacity="0.20"
        strokeWidth="6"
        strokeLinecap="round"
      />
    </svg>
  )
}
