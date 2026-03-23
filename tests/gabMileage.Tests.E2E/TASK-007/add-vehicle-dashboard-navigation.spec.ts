import { expect, test } from '@playwright/test'

/**
 * TASK-007 E2E — happy path add vehicle → dashboard grid → detail navigation
 *
 * Targets the *real* running stack (Gateway + Web). No route mocking.
 *
 * Env:
 * - GABMILEAGE_BASE_URL (e.g. https://localhost:3000)
 * - GABMILEAGE_GAB_SESSION: a real gab_session JWT cookie value
 *
 * Breadcrumbs (backend-builder)
 * Who: backend-builder
 * What: Add a minimal Playwright happy-path that validates add vehicle renders on dashboard and detail.
 * Why: Acceptance requires at least one UI-level automated check for Sprint 02 vehicle slice.
 * Alternatives considered: Reusing existing TASK-004/TASK-005 specs only (rejected: task asks for targeted Sprint 02/TASK-007 coverage).
 * Confidence: medium
 */

async function newAuthedPage({
  browser,
}: {
  browser: any
}) {
  // CI-runnable auth strategy (no secrets): the Gateway can be configured to enable a test-only login endpoint
  // that mints a short-lived JWT and sets the standard `gab_session` HttpOnly cookie.
  const context = await browser.newContext()
  const page = await context.newPage()

  const userId = `pw-task007-${Date.now()}-${Math.random().toString(16).slice(2)}`
  const resp = await page.goto(
    `/api/auth/test-login?userId=${encodeURIComponent(userId)}&returnTo=${encodeURIComponent(
      '/',
    )}`,
  )

  expect(resp, 'Expected Gateway test-login endpoint to be available for CI E2E')
    .not
    .toBeNull()
  expect(resp!.status(), 'Expected /api/auth/test-login to succeed')
    .toBeGreaterThanOrEqual(200)
  expect(resp!.status(), 'Expected /api/auth/test-login to succeed').toBeLessThan(400)

  // The endpoint should redirect back to the app.
  await page.waitForURL('/')

  return { page, userId }
}

test('user can add a vehicle then sees it in the dashboard grid and can navigate to detail', async ({
  browser,
}) => {
  const { page } = await newAuthedPage({ browser })

  const name = `TASK-007 E2E ${Date.now()}-${Math.random().toString(16).slice(2)}`

  // Add a vehicle.
  await page.goto('/vehicles/new')
  await expect(page.getByTestId('add-vehicle-page')).toBeVisible()

  await page.getByLabel('Vehicle Name').fill(name)
  await page.getByRole('button', { name: /^add to garage$/i }).click()

  await page.waitForURL(/\/vehicles\/[0-9a-fA-F-]{36}$/)
  await expect(page.getByRole('heading', { name })).toBeVisible()

  const url = new URL(page.url())
  const vehicleId = url.pathname.split('/').pop()!

  // Dashboard grid should render the new vehicle.
  await page.goto('/')
  await expect(page.getByRole('heading', { name: /your garage/i })).toBeVisible()

  const list = page.getByTestId('garage-vehicle-list')
  await expect(list).toBeVisible()

  const card = list.locator('button').filter({ hasText: name }).first()
  await expect(card).toBeVisible()

  // Navigate to detail from the grid.
  await card.click()
  await page.waitForURL(`/vehicles/${vehicleId}`)
  await expect(page.getByRole('heading', { name })).toBeVisible()
})
