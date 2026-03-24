# gabMileage.Tests.E2E

Playwright E2E tests for gabMileage, written to hit the **real running stack** (no mocking).

## Prereqs
- Start the stack (Gateway + Web)
- Ensure `GABMILEAGE_BASE_URL` points at the browser-facing URL (usually the Gateway)
- Install test deps + Playwright browsers:

```bash
cd tests/gabMileage.Tests.E2E
npm ci
npx playwright install
```

## Run

```bash
cd tests/gabMileage.Tests.E2E
set GABMILEAGE_BASE_URL=https://localhost:3000
npm test
```

## Authenticated tests

Some specs require a real `gab_session` JWT (no mocking). Provide:

```bash
set GABMILEAGE_GAB_SESSION=eyJ...
```

Some specs also exercise the **real WorkOS hosted login UI**. Provide WorkOS test-tenant credentials:

```bash
set GABMILEAGE_WORKOS_EMAIL=you@example.com
set GABMILEAGE_WORKOS_PASSWORD=...
```

### Security note (WorkOS credentials)
Playwright traces/video can capture typed secrets. This repo defaults `PW_TRACE=off` in `playwright.config.ts`.
If you override trace settings for debugging, avoid doing so while running specs that type WorkOS passwords.
