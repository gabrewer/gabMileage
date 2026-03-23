/**
 * Breadcrumbs (frontend-builder)
 * What: Deterministic avatar helpers (initial + warm color) derived from vehicleId.
 * Why: Dashboard cards + vehicle header need a stable visual anchor even without photos; color must not change when name changes.
 * Alternatives considered: random colors per render (rejected; flicker), storing a color server-side (premature).
 * Confidence: high
 */

export function vehicleInitial(name: string): string {
  const trimmed = name.trim()
  const first = trimmed.length > 0 ? trimmed[0] : '?'
  return first.toUpperCase()
}

export function vehicleAvatarColor(vehicleId: string): string {
  // Simple string hash -> hue.
  let hash = 0
  for (let i = 0; i < vehicleId.length; i++) {
    hash = (hash * 31 + vehicleId.charCodeAt(i)) >>> 0
  }

  const hue = hash % 360

  // Soft, warm background that reads well behind a single letter.
  return `hsl(${hue} 70% 85%)`
}

export function vehicleAvatarTextColor(vehicleId: string): string {
  let hash = 0
  for (let i = 0; i < vehicleId.length; i++) {
    hash = (hash * 31 + vehicleId.charCodeAt(i)) >>> 0
  }

  const hue = hash % 360
  return `hsl(${hue} 45% 30%)`
}
