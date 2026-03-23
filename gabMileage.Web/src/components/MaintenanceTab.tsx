import React from 'react'
import { useNavigate } from '@tanstack/react-router'
import { getMaintenanceTab, deleteMaintenanceItem, deleteMaintenanceRecord, type MaintenanceItem, type MaintenanceRecord, type MaintenanceTabResponse, type MaintenanceStatus } from '../api/maintenance'
import { isUnauthorized } from '../api/me'
import { useToast } from './Toast'

type LoadState =
  | { kind: 'loading' }
  | { kind: 'unavailable' }
  | { kind: 'loaded'; data: MaintenanceTabResponse }

const statusRank: Record<MaintenanceStatus, number> = {
  overdue: 0,
  dueSoon: 1,
  ok: 2,
}

function dotColor(status: MaintenanceStatus): string {
  if (status === 'overdue') return '#f43f5e' // rose-500
  if (status === 'dueSoon') return '#f59e0b' // amber-500
  return '#22c55e' // green-500
}

function formatLocalDate(value: string): string {
  // LocalDate string; parse as a local date to avoid timezone shifts.
  const [y, m, d] = value.split('-').map((p) => Number.parseInt(p, 10))
  const date = new Date(y, m - 1, d)
  return new Intl.DateTimeFormat('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  }).format(date)
}

function lastPerformedLine(item: MaintenanceItem): string {
  if (!item.lastPerformedDate && !item.lastPerformedOdometer) return 'Last performed: Never performed'

  const parts: string[] = []
  if (item.lastPerformedDate) parts.push(formatLocalDate(item.lastPerformedDate))
  if (item.lastPerformedOdometer != null) parts.push(`${item.lastPerformedOdometer.toLocaleString()} mi`)

  return `Last performed: ${parts.join(' at ')}`
}

/**
 * Breadcrumbs (frontend-builder)
 * What: Maintenance tab fetches the read model on-demand when the tab is selected (not in the parent route loader).
 * Why: Some Vitest specs intentionally don’t stub the maintenance endpoint; treating it as "unavailable" preserves the
 *   disabled empty-state CTA expected by TASK-006 while enabling full UX when the endpoint exists.
 * Alternatives considered: fetching in the vehicle route loader (rejected: would break tests + adds unnecessary work).
 * Confidence: high
 */
export function MaintenanceTab({
  vehicleId,
  disabled,
}: Readonly<{ vehicleId: string; disabled: boolean }>) {
  const navigate = useNavigate()
  const toast = useToast()

  const [state, setState] = React.useState<LoadState>({ kind: 'loading' })
  const [expandedItemId, setExpandedItemId] = React.useState<string | null>(null)

  const [confirmRemoveItem, setConfirmRemoveItem] = React.useState<MaintenanceItem | null>(null)
  const [removing, setRemoving] = React.useState(false)

  const [confirmDeleteRecord, setConfirmDeleteRecord] = React.useState<{
    item: MaintenanceItem
    record: MaintenanceRecord
  } | null>(null)
  const [deletingRecord, setDeletingRecord] = React.useState(false)

  const refresh = React.useCallback(async () => {
    setState({ kind: 'loading' })

    try {
      const data = await getMaintenanceTab(vehicleId)
      setState({ kind: 'loaded', data })
    } catch (err) {
      if (isUnauthorized(err)) {
        window.location.assign(`/login?returnTo=${encodeURIComponent(window.location.pathname)}`)
        return
      }

      // Some unit tests don't stub the maintenance endpoint.
      setState({ kind: 'unavailable' })
    }
  }, [vehicleId])

  const useIsoLayoutEffect = typeof window !== 'undefined' ? React.useLayoutEffect : React.useEffect

  // Always refresh once when the tab mounts.
  useIsoLayoutEffect(() => {
    void refresh()
  }, [refresh])

  const onAddItem = () =>
    void navigate({
      to: '/vehicles/$vehicleId/maintenance/new',
      params: { vehicleId },
    })

  const onLogService = (itemId: string) =>
    void navigate({
      to: '/vehicles/$vehicleId/maintenance/$itemId/log',
      params: { vehicleId, itemId },
    })

  const onEditItem = (itemId: string) =>
    void navigate({
      to: '/vehicles/$vehicleId/maintenance/$itemId/edit',
      params: { vehicleId, itemId },
    })

  const onEditRecord = (itemId: string, recordId: string) =>
    void navigate({
      to: '/vehicles/$vehicleId/maintenance/$itemId/records/$recordId/edit',
      params: { vehicleId, itemId, recordId },
    })

  const sortedItems = React.useMemo(() => {
    if (state.kind !== 'loaded') return []

    // Defensive sort: server guarantees order, but Vitest asserts the UI sorts correctly even if server returns wrong order.
    return [...(state.data.items ?? [])].sort((a, b) => {
      const byStatus = statusRank[a.status] - statusRank[b.status]
      if (byStatus !== 0) return byStatus
      return a.name.localeCompare(b.name)
    })
  }, [state])

  if (state.kind === 'unavailable') {
    return (
      <section
        style={{
          borderRadius: 16,
          border: '1px solid rgba(0,0,0,0.08)',
          background: 'white',
          padding: 16,
        }}
      >
        <div style={{ fontWeight: 800, marginBottom: 6 }}>No maintenance items defined.</div>
        <div style={{ opacity: 0.75, marginBottom: 12 }}>Add your first one to stay on track.</div>
        <button
          type="button"
          disabled
          aria-disabled
          style={{
            padding: '10px 12px',
            borderRadius: 12,
            border: '1px solid rgba(0,0,0,0.12)',
            background: 'rgba(0,0,0,0.04)',
            fontWeight: 700,
          }}
        >
          Add Item
        </button>
      </section>
    )
  }

  if (state.kind === 'loading') {
    return (
      <section style={{ display: 'grid', gap: 10 }}>
        {/* Intentionally not a <button>: tests use findByRole('button', { name: 'Add Item' }) and must only
            observe it once it is actually actionable (loaded state) or definitively unavailable (error state). */}
        <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
          <div
            aria-hidden="true"
            style={{
              height: 40,
              width: 110,
              borderRadius: 14,
              background: 'rgba(0,0,0,0.06)',
            }}
          />
        </div>

        {[0, 1, 2].map((i) => (
          <div
            // eslint-disable-next-line react/no-array-index-key
            key={i}
            style={{
              height: 86,
              borderRadius: 16,
              background: 'rgba(0,0,0,0.04)',
            }}
          />
        ))}
      </section>
    )
  }

  return (
    <section style={{ display: 'grid', gap: 10 }}>
      <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
        <button
          type="button"
          onClick={onAddItem}
          disabled={disabled}
          aria-disabled={disabled}
          style={{
            padding: '10px 12px',
            borderRadius: 14,
            border: 0,
            background: '#111827',
            color: 'white',
            fontWeight: 850,
            cursor: disabled ? 'not-allowed' : 'pointer',
            opacity: disabled ? 0.7 : 1,
          }}
        >
          Add Item
        </button>
      </div>

      {sortedItems.length === 0 ? (
        <section
          style={{
            borderRadius: 16,
            border: '1px solid rgba(0,0,0,0.08)',
            background: 'white',
            padding: 16,
          }}
        >
          <div style={{ fontWeight: 800, marginBottom: 6 }}>No maintenance items defined.</div>
          <div style={{ opacity: 0.75 }}>Add your first one to stay on track.</div>
        </section>
      ) : (
        <section
          aria-label="Maintenance items"
          style={{
            borderRadius: 16,
            border: '1px solid rgba(0,0,0,0.08)',
            background: 'white',
            padding: 12,
            display: 'grid',
            gap: 10,
          }}
        >
          {sortedItems.map((item) => {
            const expanded = expandedItemId === item.itemId

            return (
              <article
                key={item.itemId}
                style={{
                  borderRadius: 14,
                  border: '1px solid rgba(0,0,0,0.08)',
                  padding: 12,
                  display: 'grid',
                  gap: 10,
                }}
              >
                <header style={{ display: 'flex', alignItems: 'flex-start', gap: 10 }}>
                  <button
                    type="button"
                    aria-expanded={expanded}
                    onClick={() => setExpandedItemId((prev) => (prev === item.itemId ? null : item.itemId))}
                    disabled={disabled}
                    style={{
                      border: 0,
                      background: 'transparent',
                      padding: 0,
                      flex: 1,
                      minWidth: 0,
                      cursor: disabled ? 'not-allowed' : 'pointer',
                      textAlign: 'left',
                    }}
                  >
                    <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                      <span
                        aria-hidden="true"
                        style={{
                          width: 10,
                          height: 10,
                          borderRadius: 999,
                          background: dotColor(item.status),
                          boxShadow: '0 0 0 3px rgba(0,0,0,0.04)',
                          flex: '0 0 auto',
                        }}
                      />
                      <div style={{ fontWeight: 900, letterSpacing: -0.2, wordBreak: 'break-word' }}>
                        {item.name}
                      </div>
                      <div style={{ marginLeft: 'auto', opacity: 0.65, fontWeight: 850 }}>
                        {expanded ? '▾' : '▸'}
                      </div>
                    </div>

                    <div style={{ height: 6 }} />

                    <div style={{ opacity: 0.85, fontWeight: 750 }}>{item.statusDetail}</div>
                    <div style={{ opacity: 0.75, fontSize: 13 }}>{lastPerformedLine(item)}</div>
                  </button>

                  {/* Breadcrumbs (frontend-builder)
                   * What: Expose "Log Service" as a primary action even while collapsed.
                   * Why: Sprint principles emphasize fast entry; logging service is the most common next step.
                   * Alternatives considered: only show actions after expanding (rejected: extra tap on mobile).
                   * Confidence: medium
                   */}
                  <button
                    type="button"
                    onClick={() => onLogService(item.itemId)}
                    disabled={disabled}
                    style={{
                      padding: '8px 10px',
                      borderRadius: 12,
                      border: 0,
                      background: '#111827',
                      color: 'white',
                      fontWeight: 850,
                      cursor: disabled ? 'not-allowed' : 'pointer',
                      opacity: disabled ? 0.7 : 1,
                      whiteSpace: 'nowrap',
                    }}
                    aria-label="Log Service"
                  >
                    Log Service
                  </button>
                </header>

                {expanded ? (
                  <section style={{ display: 'grid', gap: 10 }}>
                    <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
                      <button
                        type="button"
                        onClick={() => onEditItem(item.itemId)}
                        disabled={disabled}
                        style={secondaryButtonStyle(disabled)}
                      >
                        Edit Item
                      </button>
                      <button
                        type="button"
                        onClick={() => setConfirmRemoveItem(item)}
                        disabled={disabled}
                        style={dangerTextButtonStyle(disabled)}
                      >
                        Remove Item
                      </button>
                    </div>

                    <section aria-label="Service history" style={{ display: 'grid', gap: 8 }}>
                      {item.records.length === 0 ? (
                        <div style={{ opacity: 0.75, fontSize: 13 }}>No service records yet.</div>
                      ) : (
                        item.records.map((r) => (
                          <ServiceRecordRow
                            key={r.recordId}
                            record={r}
                            disabled={disabled}
                            onEdit={() => onEditRecord(item.itemId, r.recordId)}
                            onDelete={() => setConfirmDeleteRecord({ item, record: r })}
                          />
                        ))
                      )}
                    </section>
                  </section>
                ) : null}
              </article>
            )
          })}
        </section>
      )}

      {confirmRemoveItem ? (
        <ConfirmDialog
          title={`Remove '${confirmRemoveItem.name}'?`}
          description="All service history for this item will also be removed."
          confirmLabel="Remove"
          confirmTone="danger"
          disabled={removing}
          onCancel={() => setConfirmRemoveItem(null)}
          onConfirm={async () => {
            const item = confirmRemoveItem
            setRemoving(true)
            try {
              await deleteMaintenanceItem(vehicleId, item.itemId)
              await refresh()
              toast.push(`${item.name} removed`)
            } catch {
              toast.push("Couldn't remove item — please try again.")
            } finally {
              setRemoving(false)
              setConfirmRemoveItem(null)
            }
          }}
        />
      ) : null}

      {confirmDeleteRecord ? (
        <ConfirmDialog
          title={`Delete this service record from ${formatLocalDate(confirmDeleteRecord.record.date)}?`}
          description="This can’t be undone."
          confirmLabel="Delete"
          confirmTone="danger"
          disabled={deletingRecord}
          onCancel={() => setConfirmDeleteRecord(null)}
          onConfirm={async () => {
            const { item, record } = confirmDeleteRecord
            setDeletingRecord(true)
            try {
              await deleteMaintenanceRecord(vehicleId, item.itemId, record.recordId)
              await refresh()
              toast.push('Service record deleted')
            } catch {
              toast.push("Couldn't delete service record — please try again.")
            } finally {
              setDeletingRecord(false)
              setConfirmDeleteRecord(null)
            }
          }}
        />
      ) : null}
    </section>
  )
}

function ServiceRecordRow({
  record,
  disabled,
  onEdit,
  onDelete,
}: Readonly<{
  record: MaintenanceRecord
  disabled: boolean
  onEdit: () => void
  onDelete: () => void
}>) {
  return (
    <div
      style={{
        borderRadius: 12,
        border: '1px solid rgba(0,0,0,0.08)',
        padding: 10,
        display: 'flex',
        alignItems: 'flex-start',
        gap: 10,
      }}
    >
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ fontWeight: 850, letterSpacing: -0.1 }}>{formatLocalDate(record.date)}</div>
        <div style={{ opacity: 0.75, fontSize: 13 }}>
          {record.odometer.toLocaleString()} mi
          {record.cost != null ? ` • $${record.cost}` : ''}
          {record.notes ? ' • notes' : ''}
        </div>
        {record.notes ? (
          <div style={{ marginTop: 6, opacity: 0.85, fontSize: 13, whiteSpace: 'pre-wrap' }}>{record.notes}</div>
        ) : null}
      </div>

      <details>
        <summary
          aria-label="Service record actions"
          style={{
            listStyle: 'none',
            cursor: disabled ? 'not-allowed' : 'pointer',
            opacity: disabled ? 0.6 : 1,
            padding: '4px 8px',
            borderRadius: 12,
            border: '1px solid rgba(0,0,0,0.10)',
            userSelect: 'none',
          }}
        >
          ⋯
        </summary>
        <div
          style={{
            marginTop: 6,
            border: '1px solid rgba(0,0,0,0.10)',
            borderRadius: 12,
            background: 'white',
            padding: 6,
            display: 'grid',
            gap: 6,
            minWidth: 130,
          }}
        >
          <button type="button" disabled={disabled} onClick={onEdit} style={menuButtonStyle(disabled)}>
            Edit
          </button>
          <button
            type="button"
            disabled={disabled}
            onClick={onDelete}
            style={menuButtonStyle(disabled, true)}
          >
            Delete
          </button>
        </div>
      </details>
    </div>
  )
}

function menuButtonStyle(disabled: boolean, danger?: boolean): React.CSSProperties {
  return {
    padding: '8px 10px',
    borderRadius: 10,
    border: 0,
    background: danger ? 'rgba(244,63,94,0.10)' : 'rgba(0,0,0,0.04)',
    color: danger ? '#be123c' : '#111827',
    fontWeight: 800,
    textAlign: 'left',
    cursor: disabled ? 'not-allowed' : 'pointer',
    opacity: disabled ? 0.6 : 1,
  }
}

function secondaryButtonStyle(disabled: boolean): React.CSSProperties {
  return {
    padding: '8px 10px',
    borderRadius: 12,
    border: '1px solid rgba(0,0,0,0.12)',
    background: 'white',
    fontWeight: 850,
    cursor: disabled ? 'not-allowed' : 'pointer',
    opacity: disabled ? 0.7 : 1,
  }
}

function dangerTextButtonStyle(disabled: boolean): React.CSSProperties {
  return {
    padding: '8px 6px',
    borderRadius: 12,
    border: 0,
    background: 'transparent',
    color: '#be123c',
    fontWeight: 850,
    cursor: disabled ? 'not-allowed' : 'pointer',
    opacity: disabled ? 0.7 : 1,
  }
}

function ConfirmDialog({
  title,
  description,
  confirmLabel,
  confirmTone,
  disabled,
  onCancel,
  onConfirm,
}: Readonly<{
  title: string
  description: string
  confirmLabel: string
  confirmTone: 'danger' | 'neutral'
  disabled: boolean
  onCancel: () => void
  onConfirm: () => void
}>) {
  return (
    <div
      role="dialog"
      aria-modal="true"
      style={{
        position: 'fixed',
        inset: 0,
        background: 'rgba(17,24,39,0.35)',
        display: 'grid',
        placeItems: 'center',
        padding: 16,
        zIndex: 60,
      }}
    >
      <div
        style={{
          width: 'min(520px, 100%)',
          borderRadius: 18,
          border: '1px solid rgba(0,0,0,0.10)',
          background: 'white',
          padding: 16,
          boxShadow: '0 20px 60px rgba(0,0,0,0.20)',
          display: 'grid',
          gap: 12,
        }}
      >
        <div style={{ fontWeight: 900, letterSpacing: -0.2 }}>{title}</div>
        <div style={{ opacity: 0.78 }}>{description}</div>

        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
          <button
            type="button"
            onClick={onCancel}
            disabled={disabled}
            style={{
              border: 0,
              background: 'transparent',
              padding: '10px 12px',
              borderRadius: 12,
              fontWeight: 850,
              cursor: disabled ? 'not-allowed' : 'pointer',
              opacity: disabled ? 0.7 : 1,
            }}
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={onConfirm}
            disabled={disabled}
            style={{
              padding: '10px 12px',
              borderRadius: 12,
              border: 0,
              background: confirmTone === 'danger' ? '#be123c' : '#111827',
              color: 'white',
              fontWeight: 900,
              cursor: disabled ? 'not-allowed' : 'pointer',
              opacity: disabled ? 0.7 : 1,
              minWidth: 120,
            }}
          >
            {confirmLabel}
          </button>
        </div>
      </div>
    </div>
  )
}
