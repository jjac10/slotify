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

  it('pinta la disponibilidad por día: verde libre, rojo completo (deshabilitado), cerrado atenuado', async () => {
    const onSelect = vi.fn()
    render(
      <MonthCalendar
        value=""
        onSelect={onSelect}
        dayStatus={{ '2026-07-15': 'available', '2026-07-16': 'full', '2026-07-17': 'closed' }}
      />,
    )
    // Sin value, el calendario abre en el mes actual; navegamos a julio 2026 si hace falta.
    while (!screen.queryByText(/julio de 2026/i)) {
      await userEvent.click(screen.getByRole('button', { name: 'Mes siguiente' }))
    }

    const byDate = (iso: string) =>
      screen.getAllByTestId('calendar-day').find((d) => d.getAttribute('data-date') === iso)!

    expect(byDate('2026-07-15')).toHaveAttribute('data-status', 'available')
    expect(byDate('2026-07-15')).toBeEnabled()

    expect(byDate('2026-07-16')).toHaveAttribute('data-status', 'full')
    expect(byDate('2026-07-16')).toBeDisabled()

    expect(byDate('2026-07-17')).toHaveAttribute('data-status', 'closed')
    expect(byDate('2026-07-17')).toBeDisabled()

    // Un día sin entrada queda neutro y seleccionable
    expect(byDate('2026-07-20')).not.toHaveAttribute('data-status')
    await userEvent.click(byDate('2026-07-20'))
    expect(onSelect).toHaveBeenCalledWith('2026-07-20')
  })

  it('avisa del cambio de mes con año y mes 1-12 (para cargar su disponibilidad)', async () => {
    const onMonthChange = vi.fn()
    render(<MonthCalendar value="2026-07-15" onSelect={() => {}} onMonthChange={onMonthChange} />)

    await userEvent.click(screen.getByRole('button', { name: 'Mes siguiente' }))
    expect(onMonthChange).toHaveBeenCalledWith(2026, 8)

    await userEvent.click(screen.getByRole('button', { name: 'Mes anterior' }))
    expect(onMonthChange).toHaveBeenCalledWith(2026, 7)
  })
})
