import { expect, test } from '@playwright/test'

/**
 * TASK-005 E2E
 * Targets the *real* running stack (Gateway + Web). No route mocking.
 *
 * Env:
 * - GABMILEAGE_BASE_URL (e.g. https://localhost:3000)
 * - GABMILEAGE_GAB_SESSION: a real gab_session JWT cookie value for authenticated assertions
 */

async function newAuthedPage({
  browser,
  baseURL,
}: {
  browser: any
  baseURL: string
}) {
  const jwt = process.env.GABMILEAGE_GAB_SESSION
  test.skip(!jwt, 'Set GABMILEAGE_GAB_SESSION to run authenticated assertions')

  const context = await browser.newContext()
  await context.addCookies([
    {
      name: 'gab_session',
      value: jwt!,
      url: baseURL,
      httpOnly: true,
      secure: baseURL.startsWith('https://'),
      sameSite: 'Lax',
      path: '/',
    },
  ])

  return context.newPage()
}

test('user can add a vehicle (name-only) and sees the success toast on the detail page', async ({
  browser,
  baseURL,
}) => {
  const page = await newAuthedPage({ browser, baseURL: baseURL! })

  const name = `TASK-005 E2E ${Date.now()}`

  await page.goto('/vehicles/new')
  await expect(page.getByTestId('add-vehicle-page')).toBeVisible()

  await page.getByLabel('Vehicle Name').fill(name)
  await page.getByRole('button', { name: /^add to garage$/i }).click()

  await page.waitForURL(/\/vehicles\/[0-9a-fA-F-]{36}$/)
  await expect(page.getByRole('heading', { name })).toBeVisible()

  await expect(page.getByTestId('toast-viewport')).toBeVisible()
  await expect(page.getByTestId('toast')).toContainText(`${name} added to your garage!`)
})

test('double-clicking submit does not create a duplicate vehicle', async ({
  browser,
  baseURL,
}) => {
  const page = await newAuthedPage({ browser, baseURL: baseURL! })

  const name = `TASK-005 Double ${Date.now()}-${Math.random().toString(16).slice(2)}`

  await page.goto('/vehicles/new')
  await page.getByLabel('Vehicle Name').fill(name)

  const submit = page.getByRole('button', { name: /^add to garage$/i })

  // Fire two clicks quickly to mimic a fast double-tap.
  await Promise.allSettled([submit.click(), submit.click()])

  await page.waitForURL(/\/vehicles\/[0-9a-fA-F-]{36}$/)

  // Back to dashboard and assert this name appears exactly once.
  await page.goto('/')
  await expect(page.getByTestId('topbar')).toBeVisible()

  const cardMatches = page
    .locator('[data-testid="garage-vehicle-list"] button')
    .filter({ hasText: name })

  await expect(cardMatches).toHaveCount(1)
})
