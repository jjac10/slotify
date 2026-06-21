import { useState } from 'react'

interface Props {
  /** Fecha seleccionada en ISO "YYYY-MM-DD" (o '' si ninguna). */
  value: string
  /** Fecha mínima seleccionable (ISO); los días anteriores se deshabilitan. */
  min?: string
  onSelect: (iso: string) => void
}

function iso(y: number, m: number, d: number): string {
  return `${y}-${String(m + 1).padStart(2, '0')}-${String(d).padStart(2, '0')}`
}

const WEEKDAYS = ['L', 'M', 'X', 'J', 'V', 'S', 'D']

/** Calendario mensual con navegación de meses, día seleccionado y días pasados deshabilitados. */
export function MonthCalendar({ value, min, onSelect }: Props) {
  const initial = value ? new Date(`${value}T00:00:00`) : new Date()
  const [view, setView] = useState({ y: initial.getFullYear(), m: initial.getMonth() })

  const first = new Date(view.y, view.m, 1)
  const startOffset = (first.getDay() + 6) % 7 // lunes primero
  const daysInMonth = new Date(view.y, view.m + 1, 0).getDate()
  const monthLabel = first.toLocaleDateString('es-ES', { month: 'long', year: 'numeric' })

  function shift(delta: number) {
    const d = new Date(view.y, view.m + delta, 1)
    setView({ y: d.getFullYear(), m: d.getMonth() })
  }

  const cells: (number | null)[] = [
    ...Array.from({ length: startOffset }, () => null),
    ...Array.from({ length: daysInMonth }, (_, i) => i + 1),
  ]

  return (
    <div className="rounded-2xl border border-outline-variant/50 bg-surface-container-lowest p-stack-md" data-testid="month-calendar">
      <div className="mb-stack-sm flex items-center justify-between">
        <button type="button" onClick={() => shift(-1)} aria-label="Mes anterior"
          className="p-1 rounded-lg text-on-surface-variant hover:bg-surface-container-low transition-colors">
          <span className="material-symbols-outlined text-[20px]">chevron_left</span>
        </button>
        <span className="text-sm font-bold capitalize">{monthLabel}</span>
        <button type="button" onClick={() => shift(1)} aria-label="Mes siguiente"
          className="p-1 rounded-lg text-on-surface-variant hover:bg-surface-container-low transition-colors">
          <span className="material-symbols-outlined text-[20px]">chevron_right</span>
        </button>
      </div>
      <div className="grid grid-cols-7 gap-1 text-center">
        {WEEKDAYS.map((w) => (
          <span key={w} className="py-1 text-[11px] font-semibold text-on-surface-variant">{w}</span>
        ))}
        {cells.map((day, i) => {
          if (day === null) return <span key={`e${i}`} />
          const dateIso = iso(view.y, view.m, day)
          const disabled = min ? dateIso < min : false
          const selected = dateIso === value
          return (
            <button
              key={dateIso}
              type="button"
              disabled={disabled}
              data-testid="calendar-day"
              data-date={dateIso}
              onClick={() => onSelect(dateIso)}
              className={`aspect-square rounded-lg text-sm font-semibold transition-colors ${
                selected
                  ? 'bg-primary text-on-primary shadow-sm'
                  : disabled
                    ? 'text-on-surface-variant/30 cursor-not-allowed'
                    : 'text-on-surface hover:bg-primary-container/30'
              }`}
            >
              {day}
            </button>
          )
        })}
      </div>
    </div>
  )
}
