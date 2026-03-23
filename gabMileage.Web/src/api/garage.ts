import { ApiError, fetchJson } from './http'

export type GarageSummaryVehicle = {
  vehicleId: string
  name: string
  make: string | null
  model: string | null
  year: number | null
  photoUrl: string | null
  photoThumbnailUrl: string | null
  createdAt: string
}

export type GarageSummaryResponse = {
  userId: string
  vehicles: GarageSummaryVehicle[]
}

export type VehicleResponse = {
  vehicleId: string
  name: string
  make: string | null
  model: string | null
  year: number | null
  photoUrl: string | null
  photoThumbnailUrl: string | null
  createdAt: string
  updatedAt: string
}

export type AddVehicleRequest = {
  commandId: string
  name: string
  make: string | null
  model: string | null
  year: number | null
}

export type VehicleIdResponse = {
  vehicleId: string
}

export type UpdateVehicleRequest = {
  name: string
  make: string | null
  model: string | null
  year: number | null
  vehicleId?: string
}

export async function getGarageSummary(): Promise<GarageSummaryResponse> {
  // Contract: GET /api/v1/garage/summary
  return fetchJson<GarageSummaryResponse>('/api/v1/garage/summary')
}

export async function addVehicle(
  request: AddVehicleRequest,
): Promise<VehicleIdResponse> {
  // Contract: POST /api/v1/garage/vehicles
  return fetchJson<VehicleIdResponse>('/api/v1/garage/vehicles', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  })
}

export async function getVehicle(vehicleId: string): Promise<VehicleResponse> {
  // Contract: GET /api/v1/garage/vehicles/{vehicleId}
  return fetchJson<VehicleResponse>(`/api/v1/garage/vehicles/${vehicleId}`)
}

export async function updateVehicle(
  vehicleId: string,
  request: UpdateVehicleRequest,
): Promise<VehicleIdResponse> {
  // Contract: PUT /api/v1/garage/vehicles/{vehicleId}
  return fetchJson<VehicleIdResponse>(`/api/v1/garage/vehicles/${vehicleId}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ ...request, vehicleId: request.vehicleId }),
  })
}

export function isNotFoundVehicle(err: unknown): boolean {
  // Contract: 404 for not-found OR cross-user access.
  return err instanceof ApiError && (err.status === 404 || err.status === 400)
}
