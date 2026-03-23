import { expect, test } from '@playwright/test'

/**
 * TASK-004 E2E — Dashboard garage card grid populated state
 * Targets the *real* running stack (Gateway + Web). No route mocking.
 *
 * Env:
 * - GABMILEAGE_BASE_URL (e.g. https://localhost:3000)
 * - GABMILEAGE_GAB_SESSION: a real gab_session JWT cookie value for authenticated assertions
 *
 * Breadcrumbs (frontend-builder)
 * What: Validate the "Your Garage" populated dashboard shows newest-first vehicle cards and navigates on click.
 * Why: This is the core entrypoint for returning users; ordering + navigation must feel instant and correct.
 * Alternatives considered: API-level seeding of vehicles (rejected; would bypass the UI flow this sprint ships).
 * Confidence: medium
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

async function createVehicleViaUi(page: any, name: string): Promise<string> {
  await page.goto('/vehicles/new')
  await expect(page.getByTestId('add-vehicle-page')).toBeVisible()

  await page.getByLabel('Vehicle Name').fill(name)
  await page.getByRole('button', { name: /^add to garage$/i }).click()

  await page.waitForURL(/\/vehicles\/[0-9a-fA-F-]{36}$/)

  const url = new URL(page.url())
  const vehicleId = url.pathname.split('/').pop()!

  await expect(page.getByRole('heading', { name })).toBeVisible()

  return vehicleId
}

test('dashboard shows newest-first vehicle cards with deterministic initial placeholder, and clicking navigates to detail', async ({
  browser,
  baseURL,
}) => {
  const page = await newAuthedPage({ browser, baseURL: baseURL! })

  const olderName = `TASK-004 Older ${Date.now()}`
  await createVehicleViaUi(page, olderName)

  const newerName = `TASK-004 Newer ${Date.now()}-${Math.random().toString(16).slice(2)}`
  const newerId = await createVehicleViaUi(page, newerName)

  await page.goto('/')

  await expect(page.getByRole('heading', { name: /your garage/i })).toBeVisible()
  await expect(page.getByRole('button', { name: /\+\s*add vehicle/i })).toBeVisible()

  const list = page.getByTestId('garage-vehicle-list')
  await expect(list).toBeVisible()

  const cards = list.locator('button')
  await expect(cards).toHaveCount(2)

  // Newest-first ordering (created second should appear first).
  await expect(cards.nth(0)).toContainText(newerName)
  await expect(cards.nth(1)).toContainText(olderName)

  // Initial-letter colored circle placeholder exists for the newer card.
  const avatar = page.getByTestId(`vehicle-avatar-${newerId}`)
  await expect(avatar).toBeVisible()
  await expect(avatar).toContainText(newerName.trim()[0]!.toUpperCase())

  // Clicking the first card navigates to the detail route for the newer vehicle.
  await cards.nth(0).click()
  await page.waitForURL(`/vehicles/${newerId}`)
  await expect(page.getByRole('heading', { name: newerName })).toBeVisible()

  await expect(page.getByRole('link', { name: /back to garage/i })).toBeVisible()
})
