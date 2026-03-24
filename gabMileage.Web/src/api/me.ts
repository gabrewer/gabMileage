import { ApiError, fetchJson } from './http'

export type MeResponse = {
  userId: string
  displayName: string
  email: string
  avatarUrl: string | null
  createdAt: string
}

export async function getMe(): Promise<MeResponse> {
  // Contract: GET /api/v1/me
  return fetchJson<MeResponse>('/api/v1/me')
}

export function isUnauthorized(err: unknown): boolean {
  return err instanceof ApiError && err.status === 401
}
