import React from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'

import { inconsistentFuelNote, useFuelAutoCalc } from '../lib/fuelAutoCalc'

function Harness() {
  const calc = useFuelAutoCalc()

  return (
    <div>
      <label>
        Gallons
        <input
          id="field-gallons"
          aria-label="Gallons"
          value={calc.values.gallons}
          onChange={(e) => calc.setValue('gallons', e.target.value)}
          onBlur={(e) => calc.onBlur(e)}
        />
      </label>

      <label>
        Total Cost
        <input
          id="field-total-cost"
          aria-label="Total Cost"
          value={calc.values.totalCost}
          onChange={(e) => calc.setValue('totalCost', e.target.value)}
          onBlur={(e) => calc.onBlur(e)}
        />
      </label>

      <label>
        Price/gal
        <input
          id="field-price-gal"
          aria-label="Price/gal"
          value={calc.values.pricePerGallon}
          onChange={(e) => calc.setValue('pricePerGallon', e.target.value)}
          onBlur={(e) => calc.onBlur(e)}
        />
      </label>

      <button type="button" aria-label="Outside">
        Outside
      </button>

      <div data-testid="note">{calc.note ?? ''}</div>
      <div data-testid="state-gallons">{calc.states.gallons}</div>
      <div data-testid="state-totalCost">{calc.states.totalCost}</div>
      <div data-testid="state-pricePerGallon">{calc.states.pricePerGallon}</div>
      <div data-testid="userOrder">{calc.userOrder.join(',')}</div>
    </div>
  )
}

describe('TASK-007 fuel auto-calc state machine (unit)', () => {
  it('two fields -> calculates the third on blur', async () => {
    render(<Harness />)
    const user = userEvent.setup()

    const gallons = screen.getByLabelText('Gallons')
    const totalCost = screen.getByLabelText('Total Cost')
    const price = screen.getByLabelText('Price/gal')

    await user.type(gallons, '10')
    await user.type(totalCost, '40')

    // Leave the trio to trigger evaluation.
    await user.click(screen.getByRole('button', { name: 'Outside' }))

    expect((price as HTMLInputElement).value).toBe('4')
    expect(screen.getByTestId('state-pricePerGallon')).toHaveTextContent('calculated')
    expect(screen.getByTestId('userOrder')).toHaveTextContent('gallons,totalCost')
  })

  it('LIFO reassignment: editing the previously-calculated field reassigns which field is calculated', async () => {
    render(<Harness />)
    const user = userEvent.setup()

    const gallons = screen.getByLabelText('Gallons')
    const totalCost = screen.getByLabelText('Total Cost')
    const price = screen.getByLabelText('Price/gal')

    await user.type(gallons, '10')
    await user.type(totalCost, '40')
    await user.click(screen.getByRole('button', { name: 'Outside' }))

    // price is calculated; user overrides it.
    await user.clear(price)
    await user.type(price, '5')

    await user.click(screen.getByRole('button', { name: 'Outside' }))

    expect((price as HTMLInputElement).value).toBe('5')
    expect((gallons as HTMLInputElement).value).toBe('8')
    expect(screen.getByTestId('state-gallons')).toHaveTextContent('calculated')
  })

  it('inconsistent triple-input: prioritizes gallons+totalCost and recalculates price/gal + shows note', async () => {
    render(<Harness />)
    const user = userEvent.setup()

    const gallons = screen.getByLabelText('Gallons')
    const totalCost = screen.getByLabelText('Total Cost')
    const price = screen.getByLabelText('Price/gal')

    await user.type(gallons, '10')
    await user.type(totalCost, '40')
    await user.type(price, '5')

    await user.click(screen.getByRole('button', { name: 'Outside' }))

    expect((price as HTMLInputElement).value).toBe('4')
    expect(screen.getByTestId('note')).toHaveTextContent(inconsistentFuelNote)
    expect(screen.getByTestId('state-pricePerGallon')).toHaveTextContent('calculated')
  })
})
