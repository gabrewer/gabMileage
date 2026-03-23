import React from 'react'

export function DashboardSkeleton() {
  return (
    <section
      data-testid="dashboard-skeleton"
      style={{ maxWidth: 1100, margin: '0 auto', padding: 16 }}
    >
      <div
        style={{
          height: 18,
          width: 220,
          borderRadius: 999,
          background: 'rgba(0,0,0,0.06)',
        }}
      />
      <div style={{ height: 12 }} />
      {/*
        Breadcrumbs:
        - Who: frontend-builder
        - What: Skeleton grid matches the explicit 1/2/3-column responsive contract
        - Why: Prevents inconsistent loading layout vs populated layout; satisfies TASK-004 Destroyer contract
        - Alternatives considered: auto-fit/auto-fill gridTemplateColumns; separate skeleton-only columns
        - Confidence: high
      */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
        {Array.from({ length: 6 }).map((_, i) => (
          <div
            // eslint-disable-next-line react/no-array-index-key
            key={i}
            style={{
              borderRadius: 16,
              border: '1px solid rgba(0,0,0,0.08)',
              background: 'white',
              padding: 14,
            }}
          >
            <div
              style={{
                height: 14,
                width: '60%',
                borderRadius: 999,
                background: 'rgba(0,0,0,0.06)',
              }}
            />
            <div style={{ height: 10 }} />
            <div
              style={{
                height: 10,
                width: '40%',
                borderRadius: 999,
                background: 'rgba(0,0,0,0.05)',
              }}
            />
          </div>
        ))}
      </div>
    </section>
  )
}
