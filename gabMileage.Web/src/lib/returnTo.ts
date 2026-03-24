/**
 * Breadcrumbs (review-agent)
 * Who: review-agent
 * What: sanitize returnTo values used for login redirects
 * Why: prevent open-redirect/phishing vectors; only allow app-internal paths
 * Confidence: high
 */

const DEFAULT_RETURN_TO = '/'

export function sanitizeReturnTo(value: unknown): string {
  if (typeof value !== 'string') return DEFAULT_RETURN_TO

  const s = value.trim()
  if (!s) return DEFAULT_RETURN_TO

  // Disallow obvious absolute URLs / protocol-relative URLs
  if (s.startsWith('http://') || s.startsWith('https://') || s.startsWith('//')) {
    return DEFAULT_RETURN_TO
  }

  // Only allow internal paths. Query-only or hash-only values are rejected.
  if (!s.startsWith('/')) return DEFAULT_RETURN_TO

  // Avoid weird path parsing differences across environments
  if (s.includes('\\')) return DEFAULT_RETURN_TO

  // Avoid redirect loops back into the redirect trampoline
  if (s === '/auth/redirect' || s.startsWith('/auth/redirect?')) {
    return DEFAULT_RETURN_TO
  }

  return s
}

/**
 * Convert a router/browser href (which might be absolute) into a safe, app-internal returnTo.
 */
export function returnToFromHref(href: string): string {
  if (!href) return DEFAULT_RETURN_TO

  if (href.startsWith('http://') || href.startsWith('https://')) {
    try {
      const u = new URL(href)
      return sanitizeReturnTo(`${u.pathname}${u.search}${u.hash}`)
    } catch {
      return DEFAULT_RETURN_TO
    }
  }

  return sanitizeReturnTo(href)
}
