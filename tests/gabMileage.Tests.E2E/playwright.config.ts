import { defineConfig, devices } from '@playwright/test'

const baseURL = process.env.GABMILEAGE_BASE_URL ?? 'http://localhost:3000'

type TraceMode = 'off' | 'on' | 'retain-on-failure' | 'on-first-retry'

function getTraceMode(): TraceMode {
  const raw = (process.env.PW_TRACE ?? 'off').trim()
  if (
    raw === 'off' ||
    raw === 'on' ||
    raw === 'retain-on-failure' ||
    raw === 'on-first-retry'
  ) {
    return raw
  }

  return 'off'
}

export default defineConfig({
  testDir: './',
  timeout: 30_000,
  expect: { timeout: 5_000 },
  use: {
    baseURL,

    // Local Aspire/dev commonly uses untrusted HTTPS dev-certs.
    ignoreHTTPSErrors: true,

    // Default to *no trace artifacts* to avoid credential leakage when specs
    // exercise real hosted auth (WorkOS) with typed secrets.
    trace: getTraceMode(),

    // Avoid video artifacts by default; they can also capture secrets.
    video: 'off',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
})
