import React from 'react'
import { uuidV4 } from '../lib/uuid'

type ToastItem = {
  id: string
  message: string
}

type ToastContextValue = {
  push: (message: string) => void
}

const ToastContext = React.createContext<ToastContextValue | null>(null)

/**
 * Breadcrumbs (frontend-builder)
 * What: Tiny in-app toast system for cross-page success feedback (Add vehicle, Save changes).
 * Why: Sprint 02 UX calls for success toasts that survive navigation (add -> detail).
 * Alternatives considered: query-string "toast" param (leaky URLs), window.alert (jarring), adding a full UI library (too heavy).
 * Confidence: medium
 */
export function ToastProvider({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  const [items, setItems] = React.useState<ToastItem[]>([])

  const push = React.useCallback((message: string) => {
    // Use UUIDs so toast keys remain stable even when environments stub crypto.
    const id = uuidV4()

    setItems((prev) => [...prev, { id, message }])
  }, [])

  // Auto-dismiss oldest toast.
  React.useEffect(() => {
    if (items.length === 0) return

    const t = setTimeout(() => {
      setItems((prev) => prev.slice(1))
    }, 4_000)

    return () => clearTimeout(t)
  }, [items])

  return (
    <ToastContext.Provider value={{ push }}>
      {children}
      <ToastViewport items={items} />
    </ToastContext.Provider>
  )
}

export function useToast(): ToastContextValue {
  const ctx = React.useContext(ToastContext)
  if (!ctx) throw new Error('useToast must be used within <ToastProvider />')
  return ctx
}

function ToastViewport({ items }: Readonly<{ items: ToastItem[] }>) {
  if (items.length === 0) return null

  return (
    <div
      data-testid="toast-viewport"
      style={{
        position: 'fixed',
        left: 0,
        right: 0,
        bottom: 14,
        display: 'grid',
        justifyItems: 'center',
        pointerEvents: 'none',
        zIndex: 50,
        padding: '0 12px',
      }}
    >
      <div style={{ display: 'grid', gap: 8, width: 'min(520px, 100%)' }}>
        {items.map((t) => (
          <div
            key={t.id}
            data-testid="toast"
            role="status"
            aria-live="polite"
            style={{
              pointerEvents: 'auto',
              borderRadius: 16,
              border: '1px solid rgba(0,0,0,0.10)',
              background: 'rgba(17,24,39,0.96)',
              color: 'white',
              padding: '10px 12px',
              boxShadow: '0 10px 30px rgba(0,0,0,0.20)',
              fontWeight: 650,
            }}
          >
            {t.message}
          </div>
        ))}
      </div>
    </div>
  )
}
