import { ApiError, fetchJson } from './http'

/**
 * Breadcrumbs (frontend-builder)
 * What: Typed HTTP client for Sprint 04 maintenance endpoints (MaintenanceTabResponse + item/record mutations).
 * Why: Keeps UI/tests anchored to docs/api/maintenance-tracking.md; prevents endpoint drift and ensures SSR-friendly fetch.
 * Alternatives considered: inlining fetch calls (rejected: harder to keep consistent with contract).
 * Confidence: high
 */

export type MaintenanceStatus = 'ok' | 'dueSoon' | 'overdue'

export type MaintenanceRecord = {
  recordId: string
  itemId: string
  date: string
  odometer: number
  cost: number | null
  notes: string | null
  createdAt: string
}

export type MaintenanceItem = {
  itemId: string
  name: string

  mileageInterval: number | null
  timeIntervalMonths: number | null
  mileageWarning: number
  timeWarningDays: number

  status: MaintenanceStatus
  statusDetail: string
  lastPerformedDate: string | null
  lastPerformedOdometer: number | null

  records: MaintenanceRecord[]
  createdAt: string
}

export type MaintenanceTabResponse = {
  vehicleId: string
  items: MaintenanceItem[]
  lastOdometer: number | null
}

export type DefineMaintenanceItemRequest = {
  commandId: string
  name: string
  mileageInterval: number | null
  timeIntervalMonths: number | null
  mileageWarning?: number
  timeWarningDays?: number
  initialLastPerformedDate: string | null
  initialLastPerformedOdometer: number | null
}

export type UpdateMaintenanceItemRequest = {
  vehicleId?: string
  itemId?: string

  name: string
  mileageInterval: number | null
  timeIntervalMonths: number | null
  mileageWarning: number
  timeWarningDays: number
}

export type RecordMaintenanceRequest = {
  commandId: string
  date: string
  odometer: number
  cost: number | null
  notes: string | null
}

export type UpdateMaintenanceRecordRequest = {
  vehicleId?: string
  itemId?: string
  recordId?: string

  date: string
  odometer: number
  cost: number | null
  notes: string | null
}

export type ItemIdResponse = { itemId: string }
export type RecordIdResponse = { recordId: string }

export async function getMaintenanceTab(vehicleId: string): Promise<MaintenanceTabResponse> {
  // Contract: GET /api/v1/garage/vehicles/{vehicleId}/maintenance
  return fetchJson<MaintenanceTabResponse>(`/api/v1/garage/vehicles/${vehicleId}/maintenance`)
}

export async function defineMaintenanceItem(
  vehicleId: string,
  request: DefineMaintenanceItemRequest,
): Promise<ItemIdResponse> {
  // Contract: POST /api/v1/garage/vehicles/{vehicleId}/maintenance/items
  return fetchJson<ItemIdResponse>(`/api/v1/garage/vehicles/${vehicleId}/maintenance/items`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  })
}

export async function updateMaintenanceItem(
  vehicleId: string,
  itemId: string,
  request: UpdateMaintenanceItemRequest,
): Promise<ItemIdResponse> {
  // Contract: PUT /api/v1/garage/vehicles/{vehicleId}/maintenance/items/{itemId}
  return fetchJson<ItemIdResponse>(
    `/api/v1/garage/vehicles/${vehicleId}/maintenance/items/${itemId}`,
    {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ ...request, vehicleId: request.vehicleId, itemId: request.itemId }),
    },
  )
}

export async function deleteMaintenanceItem(vehicleId: string, itemId: string): Promise<void> {
  // Contract: DELETE /api/v1/garage/vehicles/{vehicleId}/maintenance/items/{itemId}
  await fetchJson<unknown>(
    `/api/v1/garage/vehicles/${vehicleId}/maintenance/items/${itemId}`,
    { method: 'DELETE' },
  )
}

export async function recordMaintenance(
  vehicleId: string,
  itemId: string,
  request: RecordMaintenanceRequest,
): Promise<RecordIdResponse> {
  // Contract: POST /api/v1/garage/vehicles/{vehicleId}/maintenance/items/{itemId}/records
  return fetchJson<RecordIdResponse>(
    `/api/v1/garage/vehicles/${vehicleId}/maintenance/items/${itemId}/records`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    },
  )
}

export async function updateMaintenanceRecord(
  vehicleId: string,
  itemId: string,
  recordId: string,
  request: UpdateMaintenanceRecordRequest,
): Promise<RecordIdResponse> {
  // Contract: PUT /api/v1/garage/vehicles/{vehicleId}/maintenance/items/{itemId}/records/{recordId}
  return fetchJson<RecordIdResponse>(
    `/api/v1/garage/vehicles/${vehicleId}/maintenance/items/${itemId}/records/${recordId}`,
    {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        ...request,
        vehicleId: request.vehicleId,
        itemId: request.itemId,
        recordId: request.recordId,
      }),
    },
  )
}

export async function deleteMaintenanceRecord(
  vehicleId: string,
  itemId: string,
  recordId: string,
): Promise<void> {
  // Contract: DELETE /api/v1/garage/vehicles/{vehicleId}/maintenance/items/{itemId}/records/{recordId}
  await fetchJson<unknown>(
    `/api/v1/garage/vehicles/${vehicleId}/maintenance/items/${itemId}/records/${recordId}`,
    { method: 'DELETE' },
  )
}

export function isNotFoundMaintenance(err: unknown): boolean {
  // Contract: 404 for not-found OR cross-user access.
  // Also treat 400 as not-found-ish for invalid UUIDs (matches other API helpers in this repo).
  return err instanceof ApiError && (err.status === 404 || err.status === 400)
}
