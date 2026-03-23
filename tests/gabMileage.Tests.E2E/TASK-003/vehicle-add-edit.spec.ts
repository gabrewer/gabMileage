import { expect, test } from '@playwright/test'

/**
 * TASK-003 E2E
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

test('authenticated user can add a vehicle and lands on detail page', async ({
  browser,
  baseURL,
}) => {
  const page = await newAuthedPage({ browser, baseURL: baseURL! })

  const name = `E2E ${Date.now()}`

  await page.goto('/')
  await expect(page.getByTestId('topbar')).toBeVisible()

  await page.getByRole('button', { name: /^add vehicle$/i }).click()
  await page.waitForURL('**/vehicles/new')

  await expect(page.getByTestId('add-vehicle-page')).toBeVisible()

  await page.getByLabel('Vehicle Name').fill(name)
  await page.getByLabel('Make').fill('Honda')
  await page.getByLabel('Model').fill('Civic')
  await page.getByLabel('Year').fill('2019')

  await page.getByRole('button', { name: /^add to garage$/i }).click()

  await page.waitForURL(/\/vehicles\/[0-9a-fA-F-]{36}$/)
  await expect(page.getByRole('heading', { name })).toBeVisible()

  // Success toast should be visible briefly.
  await expect(page.getByTestId('toast-viewport')).toBeVisible()
  await expect(page.getByTestId('toast')).toContainText(
    `${name} added to your garage!`,
  )
})

test('add vehicle form shows Sprint 02 validation messages', async ({
  browser,
  baseURL,
}) => {
  const page = await newAuthedPage({ browser, baseURL: baseURL! })

  await page.goto('/vehicles/new')
  await expect(page.getByTestId('add-vehicle-form')).toBeVisible()

  // Submit empty -> name required.
  await page.getByRole('button', { name: /^add to garage$/i }).click()
  await expect(page.getByText('Your vehicle needs a name')).toBeVisible()

  // Invalid year -> year message.
  await page.getByLabel('Vehicle Name').fill('Test')
  await page.getByLabel('Year').fill('1899')
  await page.getByRole('button', { name: /^add to garage$/i }).click()
  await expect(page.getByText('Enter a valid year')).toBeVisible()
})

test('authenticated user can edit vehicle profile inline and save', async ({
  browser,
  baseURL,
}) => {
  const page = await newAuthedPage({ browser, baseURL: baseURL! })

  const name = `E2E Edit ${Date.now()}`

  await page.goto('/vehicles/new')
  await page.getByLabel('Vehicle Name').fill(name)
  await page.getByRole('button', { name: /^add to garage$/i }).click()

  await page.waitForURL(/\/vehicles\/[0-9a-fA-F-]{36}$/)

  await page.getByRole('button', { name: /^edit$/i }).click()
  await expect(page.getByTestId('vehicle-edit-form')).toBeVisible()

  const newName = `${name} Updated`
  await page.getByLabel('Vehicle Name').fill(newName)
  await page.getByRole('button', { name: /^save$/i }).click()

  await expect(page.getByText('Changes saved')).toBeVisible()
  await expect(page.getByRole('heading', { name: newName })).toBeVisible()
})
