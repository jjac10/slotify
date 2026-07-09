import { useState } from 'react'
import type { DayAvailabilityStatus } from '../types/api'

interface Props {
  /** Fecha seleccionada en ISO "YYYY-MM-DD" (o '' si ninguna). */
  value: string
  /** Fecha mínima seleccionable (ISO); los días anteriores se deshabilitan. */
  min?: string
  onSelect: (iso: string) => void
  /**
   * Estado por día (ISO → 'available' | 'full' | 'closed') para pintar el
   * calendario estilo Booksy: punto verde = quedan huecos, punto rojo = completo
   * (no seleccionable), atenuado = cerrado. Los días sin entrada quedan neutros.
   */
  dayStatus?: Record<string, DayAvailabilityStatus>
  /** Avisa al navegar de mes (para cargar la disponibilidad de ese mes). */
  onMonthChange?: (year: number, month: number) => void
}

function iso(y: number, m: number, d: number): string {
  return `${y}-${String(m + 1).padStart(2, '0')}-${String(d).padStart(2, '0')}`
}

const WEEKDAYS = ['L', 'M', 'X', 'J', 'V', 'S', 'D']

/** Calendario mensual con navegación de meses, día seleccionado y días pasados deshabilitados. */
export function MonthCalendar({ value, min, onSelect, dayStatus, onMonthChange }: Props) {
  const initial = value ? new Date(`${value}T00:00:00`) : new Date()
  const [view, setView] = useState({ y: initial.getFullYear(), m: initial.getMonth() })

  const first = new Date(view.y, view.m, 1)
  const startOffset = (first.getDay() + 6) % 7 // lunes primero
  const daysInMonth = new Date(view.y, view.m + 1, 0).getDate()
  const monthLabel = first.toLocaleDateString('es-ES', { month: 'long', year: 'numeric' })

  function shift(delta: number) {
    const d = new Date(view.y, view.m + delta, 1)
    setView({ y: d.getFullYear(), m: d.getMonth() })
    onMonthChange?.(d.getFullYear(), d.getMonth() + 1) // mes 1-12, como la API
  }

  const cells: (number | null)[] = [
    ...Array.from({ length: startOffset }, () => null),
    ...Array.from({ length: daysInMonth }, (_, i) => i + 1),
  ]

  return (
    <div className="w-full max-w-[17rem] rounded-xl border border-outline-variant/50 bg-surface-container-lowest p-stack-sm" data-testid="month-calendar">
      <div className="mb-1 flex items-center justify-between">
        <button type="button" onClick={() => shift(-1)} aria-label="Mes anterior"
          className="p-0.5 rounded-lg text-on-surface-variant hover:bg-surface-container-low transition-colors">
          <span className="material-symbols-outlined text-[18px]">chevron_left</span>
        </button>
        <span className="text-xs font-bold capitalize">{monthLabel}</span>
        <button type="button" onClick={() => shift(1)} aria-label="Mes siguiente"
          className="p-0.5 rounded-lg text-on-surface-variant hover:bg-surface-container-low transition-colors">
          <span className="material-symbols-outlined text-[18px]">chevron_right</span>
        </button>
      </div>
      <div className="grid grid-cols-7 gap-0.5 text-center">
        {WEEKDAYS.map((w) => (
          <span key={w} className="text-[10px] font-semibold text-on-surface-variant">{w}</span>
        ))}
        {cells.map((day, i) => {
          if (day === null) return <span key={`e${i}`} />
          const dateIso = iso(view.y, view.m, day)
          const pastDay = min ? dateIso < min : false
          const status = pastDay ? undefined : dayStatus?.[dateIso]
          // Completo o cerrado: no hay nada que elegir → no seleccionable.
          const disabled = pastDay || status === 'full' || status === 'closed'
          const selected = dateIso === value
          return (
            <button
              key={dateIso}
              type="button"
              disabled={disabled}
              data-testid="calendar-day"
              data-date={dateIso}
              data-status={status}
              onClick={() => onSelect(dateIso)}
              className={`relative h-8 rounded-md text-xs font-semibold transition-colors ${
                selected
                  ? 'bg-primary text-on-primary'
                  : pastDay || status === 'closed'
                    ? 'text-on-surface-variant/30 cursor-not-allowed'
                    : status === 'full'
                      ? 'text-error/70 cursor-not-allowed'
                      : 'text-on-surface hover:bg-primary-container/30'
              }`}
            >
              {day}
              {!selected && (status === 'available' || status === 'full') && (
                <span
                  aria-hidden
                  className={`absolute bottom-0.5 left-1/2 h-1 w-1 -translate-x-1/2 rounded-full ${
                    status === 'available' ? 'bg-emerald-500' : 'bg-error'
                  }`}
                />
              )}
            </button>
          )
        })}
      </div>
    </div>
  )
}
