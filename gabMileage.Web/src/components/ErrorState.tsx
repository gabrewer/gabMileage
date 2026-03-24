import React from 'react'

export function ErrorState({
  title,
  message,
  action,
}: Readonly<{
  title: string
  message: string
  action?: React.ReactNode
}>) {
  return (
    <section
      role="alert"
      style={{
        borderRadius: 18,
        border: '1px solid rgba(0,0,0,0.10)',
        background: 'rgba(251,113,133,0.06)',
        padding: 16,
      }}
    >
      <div style={{ fontWeight: 900 }}>{title}</div>
      <div style={{ marginTop: 6, opacity: 0.8, lineHeight: 1.4 }}>{message}</div>
      {action ? <div style={{ marginTop: 12 }}>{action}</div> : null}
    </section>
  )
}
