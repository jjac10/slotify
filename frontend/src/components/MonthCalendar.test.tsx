import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MonthCalendar } from './MonthCalendar'

describe('MonthCalendar', () => {
  it('muestra el mes de la fecha seleccionada con todos sus días', () => {
    render(<MonthCalendar value="2026-07-15" onSelect={() => {}} />)

    expect(screen.getByText(/julio de 2026/i)).toBeInTheDocument()
    expect(screen.getAllByTestId('calendar-day')).toHaveLength(31)
  })

  it('deshabilita los días anteriores a min y permite seleccionar los demás', async () => {
    const onSelect = vi.fn()
    render(<MonthCalendar value="2026-07-15" min="2026-07-10" onSelect={onSelect} />)

    const days = screen.getAllByTestId('calendar-day')
    const day9 = days.find((d) => d.getAttribute('data-date') === '2026-07-09')!
    const day20 = days.find((d) => d.getAttribute('data-date') === '2026-07-20')!

    expect(day9).toBeDisabled()

    await userEvent.click(day20)
    expect(onSelect).toHaveBeenCalledWith('2026-07-20')
    expect(onSelect).toHaveBeenCalledTimes(1)
  })

  it('marca visualmente el día seleccionado', () => {
    render(<MonthCalendar value="2026-07-15" onSelect={() => {}} />)

    const selected = screen
      .getAllByTestId('calendar-day')
      .find((d) => d.getAttribute('data-date') === '2026-07-15')!
    expect(selected.className).toContain('bg-primary')
  })

  it('navega a los meses anterior y siguiente', async () => {
    render(<MonthCalendar value="2026-07-15" onSelect={() => {}} />)

    await userEvent.click(screen.getByRole('button', { name: 'Mes siguiente' }))
    expect(screen.getByText(/agosto de 2026/i)).toBeInTheDocument()
    // Agosto de 2026 tiene 31 días y empieza en sábado (offset 5)
    expect(screen.getAllByTestId('calendar-day')).toHaveLength(31)

    await userEvent.click(screen.getByRole('button', { name: 'Mes anterior' }))
    await userEvent.click(screen.getByRole('button', { name: 'Mes anterior' }))
    expect(screen.getByText(/junio de 2026/i)).toBeInTheDocument()
    expect(screen.getAllByTestId('calendar-day')).toHaveLength(30)
  })

  it('cruza de diciembre a enero del año siguiente', async () => {
    render(<MonthCalendar value="2026-12-01" onSelect={() => {}} />)

    await userEvent.click(screen.getByRole('button', { name: 'Mes siguiente' }))
    expect(screen.getByText(/enero de 2027/i)).toBeInTheDocument()
  })
})
