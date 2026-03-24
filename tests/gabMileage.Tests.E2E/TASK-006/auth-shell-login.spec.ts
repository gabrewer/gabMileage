import { expect, test } from '@playwright/test'

/**
 * TASK-006 E2E
 * Targets the *real* running stack (Gateway + Web). No route mocking.
 *
 * Env:
 * - GABMILEAGE_BASE_URL (e.g. https://localhost:17071)
 * - GABMILEAGE_GAB_SESSION (optional) : a real gab_session JWT for authenticated assertions
 */

test('unauthenticated user sees login screen at /', async ({ page }) => {
  await page.goto('/')

  // When unauthenticated, the app redirects to /login client-side after /api/v1/me returns 401.
  await expect(page.getByTestId('login-screen')).toBeVisible()
  await expect(page.getByRole('button', { name: /sign in/i })).toHaveCount(1)
})

test('auth redirect route shows "Signing you in…" loading screen', async ({ page }) => {
  await page.goto('/auth/redirect?returnTo=/')

  await expect(page.getByTestId('auth-redirect-screen')).toBeVisible()
  await expect(page.getByText(/signing you in…/i)).toBeVisible()
})

test('authenticated user sees top bar + empty garage state (requires real gab_session)', async ({ browser, baseURL }) => {
  const jwt = process.env.GABMILEAGE_GAB_SESSION
  test.skip(!jwt, 'Set GABMILEAGE_GAB_SESSION to run authenticated assertions')

  const context = await browser.newContext()
  await context.addCookies([
    {
      name: 'gab_session',
      value: jwt!,
      url: baseURL!,
      httpOnly: true,
      secure: baseURL?.startsWith('https://') ?? false,
      sameSite: 'Lax',
      path: '/',
    },
  ])

  const page = await context.newPage()
  await page.goto('/')

  await expect(page.getByTestId('topbar')).toBeVisible()
  await expect(page.getByTestId('empty-garage-state')).toBeVisible()

  await page.getByTestId('user-menu-trigger').click()
  await expect(page.getByRole('menu')).toBeVisible()

  // Sign out should clear the cookie and return to login
  await page.getByRole('menuitem', { name: /sign out/i }).click()
  await expect(page.getByTestId('login-screen')).toBeVisible()
})
