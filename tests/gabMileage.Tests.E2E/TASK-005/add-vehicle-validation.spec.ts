import { expect, test } from '@playwright/test'

/**
 * TASK-005 E2E (validation)
 * Targets the *real* running stack (Gateway + Web). No route mocking.
 *
 * Env:
 * - GABMILEAGE_BASE_URL (e.g. https://localhost:3000)
 * - GABMILEAGE_GAB_SESSION: a real gab_session JWT cookie value
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

test('whitespace-only name shows the inline validation message', async ({
  browser,
  baseURL,
}) => {
  const page = await newAuthedPage({ browser, baseURL: baseURL! })

  await page.goto('/vehicles/new')
  await expect(page.getByTestId('add-vehicle-page')).toBeVisible()

  await page.getByLabel('Vehicle Name').fill('   ')
  await page.getByRole('button', { name: /^add to garage$/i }).click()

  await expect(page.getByText('Your vehicle needs a name')).toBeVisible()
})
