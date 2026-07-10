import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { WeeklyHours } from './WeeklyHours'
import type { BusinessHour } from '../types/api'

// dayOfWeek en convención .NET: 0=domingo … 6=sábado.
const HOURS: BusinessHour[] = [
  { dayOfWeek: 1, isClosed: false, openingTime: '09:00:00', closingTime: '17:00:00' },
  { dayOfWeek: 2, isClosed: false, openingTime: '10:30:00', closingTime: '14:00:00' },
  { dayOfWeek: 6, isClosed: true, openingTime: null, closingTime: null },
]

describe('WeeklyHours', () => {
  it('muestra los 7 días de lunes a domingo con su franja u "Cerrado"', () => {
    render(<WeeklyHours hours={HOURS} />)

    const rows = screen.getAllByTestId('weekly-hours-day')
    expect(rows).toHaveLength(7)
    // Empieza en lunes y termina en domingo
    expect(rows[0]).toHaveTextContent('Lunes')
    expect(rows[0]).toHaveTextContent('09:00–17:00')
    expect(rows[1]).toHaveTextContent('Martes')
    expect(rows[1]).toHaveTextContent('10:30–14:00')
    // Sábado marcado cerrado explícitamente; los días sin registro también son "Cerrado"
    expect(rows[5]).toHaveTextContent('Sábado')
    expect(rows[5]).toHaveTextContent('Cerrado')
    expect(rows[6]).toHaveTextContent('Domingo')
    expect(rows[6]).toHaveTextContent('Cerrado')
  })

  it('resalta el día de hoy', () => {
    render(<WeeklyHours hours={HOURS} />)

    const todayDow = new Date().getDay()
    const rows = screen.getAllByTestId('weekly-hours-day')
    const highlighted = rows.filter((r) => r.dataset.today === 'true')
    expect(highlighted).toHaveLength(1)
    // La fila resaltada corresponde al día de hoy (orden L..D)
    const expectedIndex = (todayDow + 6) % 7
    expect(rows[expectedIndex].dataset.today).toBe('true')
  })
})
