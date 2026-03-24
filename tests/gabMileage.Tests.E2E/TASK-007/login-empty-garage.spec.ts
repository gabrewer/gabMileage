import { expect, test } from '@playwright/test'

// This spec types real hosted-auth credentials. Never retain artifacts that can capture secrets.
test.use({ trace: 'off', video: 'off', screenshot: 'off' })

/**
 * TASK-007 E2E smoke
 *
 * Exercises the full browser flow:
 * unauthenticated -> login screen -> "Sign in" (Gateway redirects to WorkOS) -> authenticate -> back to app -> dashboard.
 *
 * Env:
 * - GABMILEAGE_BASE_URL (e.g. https://localhost:3000)
 * - GABMILEAGE_WORKOS_EMAIL / GABMILEAGE_WORKOS_PASSWORD : credentials in the configured WorkOS test tenant
 *
 * Breadcrumbs (backend-builder)
 * Who: backend-builder
 * What: Use the real WorkOS hosted login UI in smoke E2E (no route mocking).
 * Why: The repo’s auth contract is Gateway ⇄ WorkOS; no dev/test auth bypass exists in app code today.
 * Alternatives considered: (1) injecting a `gab_session` JWT cookie directly (doesn’t validate the login UX),
 *   (2) adding a `/dev-login` test auth endpoint (would change product behavior/surface area).
 * Confidence: medium
 */

test('smoke: login -> dashboard shows empty garage heading + CTA', async ({ page }) => {
  const email = process.env.GABMILEAGE_WORKOS_EMAIL ?? ''
  const password = process.env.GABMILEAGE_WORKOS_PASSWORD ?? ''

  test.skip(
    !email || !password,
    'Set GABMILEAGE_WORKOS_EMAIL and GABMILEAGE_WORKOS_PASSWORD to run the WorkOS-hosted login smoke test',
  )

  await page.goto('/')

  await expect(page.getByTestId('login-screen')).toBeVisible()

  // Initiates GET /api/auth/login which 302s to WorkOS hosted auth.
  await page.getByRole('button', { name: /^sign in$/i }).click()

  // WorkOS hosted auth can involve multiple redirects and/or slightly different hostnames.
  await page.waitForURL(/workos|\/auth\/(redirect|error)/i, { timeout: 30_000 })

  // If the browser somehow already has a WorkOS session, it may bounce straight back.
  if (/\/auth\/(redirect|error)/i.test(page.url())) {
    // Continue below.
  } else {
    // WorkOS hosted login UI can vary slightly; use robust locators.
    const emailInput = page
      .locator('input[type="email"]')
      .or(page.getByLabel(/email/i))
      .or(page.getByPlaceholder(/email/i))
      .first()

    if (await emailInput.count()) {
      await emailInput.fill(email)
    }

    const passwordInput = page
      .locator('input[type="password"]')
      .or(page.getByLabel(/password/i))
      .or(page.getByPlaceholder(/password/i))
      .first()

    if (await passwordInput.count()) {
      await passwordInput.fill(password)
    }

    // Most hosted auth screens use a primary submit button with one of these names.
    const submit = page
      .getByRole('button', { name: /sign in|log in|continue|submit/i })
      .first()

    await submit.click()

    // After successful auth, WorkOS returns to the Gateway callback which redirects to /auth/redirect.
    await page.waitForURL(/\/auth\/(redirect|error)/i, { timeout: 60_000 })
  }

  // Eventually we should land on the dashboard.
  await page.waitForURL((url) => url.pathname === '/', { timeout: 30_000 })

  await expect(page.getByTestId('topbar')).toBeVisible()
  await expect(page.getByTestId('empty-garage-state')).toBeVisible()

  // Acceptance: assert the empty-garage heading + CTA.
  await expect(page.getByRole('heading', { name: /your garage is empty/i })).toBeVisible()
  await expect(page.getByRole('button', { name: /\+\s*add vehicle/i })).toBeVisible()
})
