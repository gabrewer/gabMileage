/**
 * Breadcrumbs (frontend-builder)
 * What: Minimal UUIDv4 generator with crypto.randomUUID fast path.
 * Why: Command idempotency keys must be UUIDs even in limited test/runtime environments.
 * Alternatives considered: adding a uuid package (rejected; avoid new deps), Math.random string without v4 bits (rejected; tests require v4 format).
 * Confidence: medium
 */
export function uuidV4(): string {
  // Prefer the platform implementation when available.
  const cryptoObj = (globalThis as any).crypto as Crypto | undefined
  if (cryptoObj && typeof (cryptoObj as any).randomUUID === 'function') {
    return (cryptoObj as any).randomUUID() as string
  }

  // RFC4122-ish v4 using Math.random (not cryptographically secure; adequate for client idempotency keys).
  // Ensures version=4 and variant=10xx.
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0
    const v = c === 'x' ? r : (r & 0x3) | 0x8
    return v.toString(16)
  })
}
