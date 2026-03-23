import '@testing-library/jest-dom/vitest'

import { cleanup } from '@testing-library/react'
import { afterEach, beforeAll } from 'vitest'

beforeAll(() => {
  // Some runtimes expose `globalThis.crypto` as a getter-only property.
  // Tests rely on being able to temporarily override it.
  try {
    const current = (globalThis as any).crypto
    Object.defineProperty(globalThis, 'crypto', {
      value: current,
      writable: true,
      configurable: true,
    })
  } catch {
    // Best-effort. If the runtime forbids redefining `crypto`, tests will surface it.
  }
})

afterEach(() => {
  cleanup()
})
