import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { StatusPill } from './StatusPill'

describe('StatusPill', () => {
  it.each([
    ['pending', 'Pendiente'],
    ['confirmed', 'Confirmada'],
    ['cancelled', 'Cancelada'],
    ['no-show', 'No-show'],
  ])('traduce el estado %s a "%s"', (status, label) => {
    render(<StatusPill status={status} />)
    expect(screen.getByText(label)).toBeInTheDocument()
  })

  it('muestra tal cual un estado desconocido (sin romper)', () => {
    render(<StatusPill status="algo-raro" />)
    expect(screen.getByText('algo-raro')).toBeInTheDocument()
  })
})
