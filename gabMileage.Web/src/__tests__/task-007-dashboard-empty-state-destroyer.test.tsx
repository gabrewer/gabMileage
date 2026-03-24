import React from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'

import { DashboardEmptyState } from '../components/DashboardEmptyState'

/**
 * Destroyer: lock in the minimal UX contract relied on by the TASK-007 E2E smoke.
 * If these assertions start failing, the Playwright smoke will likely become flaky/brittle.
 */

describe('TASK-007 Destroyer: DashboardEmptyState', () => {
  it('renders an accessible heading + CTA and calls onAddVehicle on click', async () => {
    const onAddVehicle = vi.fn()

    render(<DashboardEmptyState onAddVehicle={onAddVehicle} />)

    // E2E relies on these being findable via roles and stable copy.
    expect(
      screen.getByRole('heading', { name: /your garage is empty/i }),
    ).toBeInTheDocument()

    const cta = screen.getByRole('button', { name: /\+\s*add vehicle/i })
    expect(cta).toBeInTheDocument()

    // Ensure the section test id remains available for additional stable assertions.
    expect(screen.getByTestId('empty-garage-state')).toBeInTheDocument()

    const user = userEvent.setup()
    await user.click(cta)

    expect(onAddVehicle).toHaveBeenCalledTimes(1)
  })
})
