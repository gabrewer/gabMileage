export class ApiError extends Error {
  readonly status: number
  readonly body: unknown

  constructor(status: number, body: unknown) {
    super(`API request failed with status ${status}`)
    this.status = status
    this.body = body
  }
}

function toServerAbsoluteUrl(input: RequestInfo | URL): RequestInfo | URL {
  // Node's fetch requires absolute URLs. In SSR we reconstruct an absolute URL using the
  // externally-facing base URL (Aspire/AppHost sets this to the Gateway URL).
  if (typeof window === 'undefined' && typeof input === 'string' && input.startsWith('/')) {
    const base =
      process.env.GABMILEAGE_BASE_URL ?? `http://localhost:${process.env.PORT ?? '3000'}`
    return new URL(input, base)
  }

  return input
}

export async function fetchJson<T>(input: RequestInfo | URL, init?: RequestInit) {
  const res = await fetch(toServerAbsoluteUrl(input), {
    ...init,
    headers: {
      ...(init?.headers ?? {}),
      Accept: 'application/json',
    },
  })

  const contentType = res.headers.get('content-type') ?? ''
  const isJson = contentType.includes('application/json')
  const body = isJson ? await res.json().catch(() => undefined) : await res.text()

  if (!res.ok) {
    throw new ApiError(res.status, body)
  }

  return body as T
}
