import { ApiError, fetchJson } from './http'

/**
 * Breadcrumbs (frontend-builder)
 * What: Typed HTTP client for Sprint 03 fuel log endpoints (FuelLog/FuelLogList)
 * Why: Keeps the UI and tests anchored to the contract in docs/api/fuel-logging.md; prevents endpoint drift.
 * Alternatives considered: (1) inlining fetch calls in routes/components (rejected: harder to keep contract consistent),
 *   (2) adding a generated OpenAPI client (rejected: no OpenAPI source in repo today).
 * Confidence: high
 */

export type FuelLogCalculatedField = 'gallons' | 'totalCost' | 'pricePerGallon' | null

export type FuelLog = {
  fuelLogId: string
  date: string
  odometer: number
  gallons: number
  totalCost: number
  pricePerGallon: number
  calculatedField: FuelLogCalculatedField
  isPartialFillUp: boolean
  notes: string | null
  mpg: number | null
  createdAt: string
}

export type FuelLogList = {
  vehicleId: string
  logs: FuelLog[]
  lastOdometer: number | null
}

export type RecordFuelLogRequest = {
  commandId: string
  date: string
  odometer: number

  gallons: number | null
  totalCost: number | null
  pricePerGallon: number | null
  calculatedField: FuelLogCalculatedField

  isPartialFillUp: boolean
  notes: string | null
}

export type FuelLogCreateResponse = {
  fuelLogId: string
  odometerWarning: string | null
}

export type UpdateFuelLogRequest = {
  vehicleId?: string
  fuelLogId?: string

  date: string
  odometer: number

  gallons: number | null
  totalCost: number | null
  pricePerGallon: number | null
  calculatedField: FuelLogCalculatedField

  isPartialFillUp: boolean
  notes: string | null
}

export type FuelLogIdResponse = {
  fuelLogId: string
}

export async function getFuelLogs(vehicleId: string): Promise<FuelLogList> {
  // Contract: GET /api/v1/garage/vehicles/{vehicleId}/fuel-logs
  return fetchJson<FuelLogList>(`/api/v1/garage/vehicles/${vehicleId}/fuel-logs`)
}

export async function recordFuelLog(
  vehicleId: string,
  request: RecordFuelLogRequest,
): Promise<FuelLogCreateResponse> {
  // Contract: POST /api/v1/garage/vehicles/{vehicleId}/fuel-logs
  return fetchJson<FuelLogCreateResponse>(
    `/api/v1/garage/vehicles/${vehicleId}/fuel-logs`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    },
  )
}

export async function updateFuelLog(
  vehicleId: string,
  fuelLogId: string,
  request: UpdateFuelLogRequest,
): Promise<FuelLogIdResponse> {
  // Contract: PUT /api/v1/garage/vehicles/{vehicleId}/fuel-logs/{fuelLogId}
  return fetchJson<FuelLogIdResponse>(
    `/api/v1/garage/vehicles/${vehicleId}/fuel-logs/${fuelLogId}`,
    {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    },
  )
}

export async function deleteFuelLog(vehicleId: string, fuelLogId: string): Promise<void> {
  // Contract: DELETE /api/v1/garage/vehicles/{vehicleId}/fuel-logs/{fuelLogId}
  await fetchJson<unknown>(
    `/api/v1/garage/vehicles/${vehicleId}/fuel-logs/${fuelLogId}`,
    { method: 'DELETE' },
  )
}

export function isNotFoundFuelLog(err: unknown): boolean {
  // Contract: 404 for not-found OR cross-user access.
  // Also treat 400 as not-found-ish for invalid UUIDs (matches other API helpers in this repo).
  return err instanceof ApiError && (err.status === 404 || err.status === 400)
}
