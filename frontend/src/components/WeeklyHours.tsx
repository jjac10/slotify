import type { BusinessHour } from '../types/api'

// Etiquetas en orden de presentación L..D; dayOfWeek en convención .NET (0=domingo).
const DAYS: Array<{ dow: number; label: string }> = [
  { dow: 1, label: 'Lunes' },
  { dow: 2, label: 'Martes' },
  { dow: 3, label: 'Miércoles' },
  { dow: 4, label: 'Jueves' },
  { dow: 5, label: 'Viernes' },
  { dow: 6, label: 'Sábado' },
  { dow: 0, label: 'Domingo' },
]

function formatTime(t: string): string {
  return t.slice(0, 5) // "09:00:00" → "09:00"
}

/**
 * Horario semanal de solo lectura (ficha pública, sobre todo negocios
 * solo-calendario: el cliente ve cuándo hay sitio antes de llamar).
 * Resalta el día de hoy.
 */
export function WeeklyHours({ hours }: { hours: BusinessHour[] }) {
  const byDow = new Map(hours.map((h) => [h.dayOfWeek, h]))
  const todayDow = new Date().getDay()

  return (
    <ul className="flex flex-col gap-0.5" data-testid="weekly-hours">
      {DAYS.map(({ dow, label }) => {
        const h = byDow.get(dow)
        const open = h && !h.isClosed && h.openingTime && h.closingTime
        const isToday = dow === todayDow
        return (
          <li
            key={dow}
            data-testid="weekly-hours-day"
            data-today={isToday ? 'true' : undefined}
            className={`flex items-center justify-between gap-2 rounded px-1.5 py-0.5 text-sm ${
              isToday ? 'bg-primary-container/15 font-semibold' : ''
            }`}
          >
            <span>{label}</span>
            <span className={open ? 'text-on-surface' : 'text-on-surface-variant'}>
              {open ? `${formatTime(h!.openingTime!)}–${formatTime(h!.closingTime!)}` : 'Cerrado'}
            </span>
          </li>
        )
      })}
    </ul>
  )
}
