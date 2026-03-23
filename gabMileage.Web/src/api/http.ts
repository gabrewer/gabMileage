export class ApiError extends Error {
  readonly status: number
  readonly body: unknown

  constructor(status: number, body: unknown) {
    super(`API request failed with status ${status}`)
    this.status = status
    this.body = body
  }
}

function getServerBaseUrl(): URL {
  const raw =
    process.env.GABMILEAGE_BASE_URL ?? `http://localhost:${process.env.PORT ?? '3000'}`
  return new URL(raw)
}

function tryGetRequestUrl(input: RequestInfo | URL): URL | null {
  try {
    if (typeof input === 'string') return new URL(input, getServerBaseUrl())
    if (input instanceof URL) return input
    if (typeof Request !== 'undefined' && input instanceof Request) return new URL(input.url)
    return null
  } catch {
    return null
  }
}

function toServerAbsoluteUrl(input: RequestInfo | URL): RequestInfo | URL {
  // Node's fetch requires absolute URLs. In SSR we reconstruct an absolute URL using the
  // externally-facing base URL (Aspire/AppHost sets this to the Gateway URL).
  if (typeof window === 'undefined' && typeof input === 'string' && input.startsWith('/')) {
    return new URL(input, getServerBaseUrl())
  }

  return input
}

async function getIncomingCookieHeader(): Promise<string | undefined> {
  // Breadcrumbs (backend-builder)
  // Who: backend-builder
  // What: Forward the incoming browser Cookie header during SSR fetches.
  // Why: Without this, SSR calls to the Gateway look anonymous and can cause redirect loops (/ -> /login).
  // Alternatives considered: disabling SSR for authenticated routes (rejected: bigger behavioral change).
  // Confidence: medium
  if (typeof window !== 'undefined') return undefined

  try {
    const mod = await import('@tanstack/start-server-core')
    // start-server-core stores the current request in AsyncLocalStorage.
    return (mod as any).getRequestHeader?.('cookie') as string | undefined
  } catch {
    return undefined
  }
}

export async function fetchJson<T>(input: RequestInfo | URL, init?: RequestInit) {
  // Vitest unit tests assert GET requests are explicit (method === 'GET').
  // Keeping this stable also makes fetch mocking less fragile.
  const method = init?.method ?? 'GET'

  const cookie = await getIncomingCookieHeader()

  const resolvedInput = toServerAbsoluteUrl(input)
  const url = tryGetRequestUrl(resolvedInput)
  const forwardCookie = !!cookie && !!url && url.origin === getServerBaseUrl().origin

  const res = await fetch(resolvedInput, {
    ...init,
    method,
    headers: {
      ...(forwardCookie ? { cookie } : {}),
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

export function isApiError(err: unknown): err is ApiError {
  return err instanceof ApiError
}

type ValidationProblemDetails = {
  errors?: Record<string, string[]>
}

export function getValidationErrors(err: unknown): Record<string, string[]> | null {
  if (!isApiError(err) || err.status !== 400) return null

  const body = err.body as ValidationProblemDetails | undefined
  if (!body || typeof body !== 'object') return null

  const errors = body.errors
  if (!errors || typeof errors !== 'object') return null

  return errors
}
