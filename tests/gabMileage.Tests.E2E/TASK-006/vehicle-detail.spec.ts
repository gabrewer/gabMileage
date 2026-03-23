import { expect, test } from '@playwright/test'

/**
 * TASK-006 E2E — Vehicle detail header + inline edit + tab scaffold
 * Targets the *real* running stack (Gateway + Web). No route mocking.
 *
 * Env:
 * - GABMILEAGE_BASE_URL (e.g. https://localhost:17071)
 * - GABMILEAGE_GAB_SESSION (optional): a real gab_session JWT for authenticated assertions
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

test('unknown vehicleId shows the 404 message + Back to garage link', async ({
  browser,
  baseURL,
}) => {
  const page = await newAuthedPage({ browser, baseURL: baseURL! })

  // Use an all-"a" UUID so it is valid, but (almost certainly) not present.
  const vehicleId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'

  await page.goto(`/vehicles/${vehicleId}`)

  await expect(
    page.getByRole('heading', {
      name: "This vehicle doesn't exist or has been removed.",
    }),
  ).toBeVisible()

  await expect(page.getByRole('link', { name: /back to garage/i })).toHaveAttribute(
    'href',
    '/',
  )
})

test('user can inline-edit vehicle profile and tabs are disabled during edit', async ({
  browser,
  baseURL,
}) => {
  const page = await newAuthedPage({ browser, baseURL: baseURL! })

  const originalName = `TASK-006 E2E ${Date.now()}`

  // Create a vehicle first (Sprint 02 contract).
  await page.goto('/vehicles/new')
  await expect(page.getByTestId('add-vehicle-page')).toBeVisible()

  await page.getByLabel('Vehicle Name').fill(originalName)
  await page.getByRole('button', { name: /^add to garage$/i }).click()

  await page.waitForURL(/\/vehicles\/[0-9a-fA-F-]{36}$/)
  await expect(page.getByRole('heading', { name: originalName })).toBeVisible()

  // Enter edit mode
  await page.getByRole('button', { name: /^edit$/i }).click()
  await expect(page.getByTestId('vehicle-edit-form')).toBeVisible()

  // Tabs should be truly disabled (a11y), not just pointer-events.
  await expect(page.getByRole('tab', { name: 'Fuel' })).toBeDisabled()
  await expect(page.getByRole('tab', { name: 'Maintenance' })).toBeDisabled()

  const updatedName = `Accord ${Date.now()}`

  await page.getByLabel('Vehicle Name').fill(updatedName)
  await page.getByLabel('Make').fill('Honda')
  await page.getByLabel('Model').fill('Accord')
  await page.getByLabel('Year').fill('2020')

  const beforeUrl = page.url()

  await page.getByRole('button', { name: /^save$/i }).click()

  // Header updates in place (no navigation).
  await expect(page).toHaveURL(beforeUrl)
  await expect(page.getByRole('heading', { name: updatedName })).toBeVisible()

  // Initial-circle placeholder should update alongside the header.
  await expect(page.getByTestId('vehicle-initial-circle')).toContainText('A')

  // Placeholder tab content exists with disabled CTAs.
  await expect(page.getByRole('button', { name: 'Log Fill-up' })).toBeDisabled()
  await page.getByRole('tab', { name: 'Maintenance' }).click()
  await expect(page.getByRole('button', { name: 'Add Item' })).toBeDisabled()
})
