import { fetchJson } from './http'

export type GarageSummaryResponse = {
  userId: string
  vehicles: unknown[]
}

export async function getGarageSummary(): Promise<GarageSummaryResponse> {
  // Contract: GET /api/v1/garage/summary
  return fetchJson<GarageSummaryResponse>('/api/v1/garage/summary')
}
