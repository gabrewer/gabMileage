import React from 'react'

export function DashboardSkeleton() {
  return (
    <main
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
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))',
          gap: 12,
        }}
      >
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
    </main>
  )
}
