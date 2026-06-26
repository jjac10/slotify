import { useMemo } from 'react'
import type { ReservationResponse } from '../types/api'

const HOUR_PX = 56
const DEFAULT_START = 8
const DEFAULT_END = 21

function localDayKey(iso: string): string {
  const d = new Date(iso)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}
function minutesOfDay(iso: string): number {
  const d = new Date(iso)
  return d.getHours() * 60 + d.getMinutes()
}
function hhmm(iso: string): string {
  return new Date(iso).toLocaleTimeString('es-ES', { hour: '2-digit', minute: '2-digit' })
}

interface Block {
  r: ReservationResponse
  top: number
  height: number
  col: number
  cols: number
}

/** Reparte en columnas las reservas que se solapan (clusters), para que no se pisen. */
function layout(items: ReservationResponse[], startHour: number): Block[] {
  const sorted = [...items].sort((a, b) => minutesOfDay(a.startTime) - minutesOfDay(b.startTime))
  const blocks: Block[] = []
  let i = 0
  while (i < sorted.length) {
    let j = i
    let maxEnd = minutesOfDay(sorted[i].endTime)
    while (j + 1 < sorted.length && minutesOfDay(sorted[j + 1].startTime) < maxEnd) {
      j++
      maxEnd = Math.max(maxEnd, minutesOfDay(sorted[j].endTime))
    }
    const cluster = sorted.slice(i, j + 1)
    const colEnds: number[] = []
    const cols = cluster.map((r) => {
      const start = minutesOfDay(r.startTime)
      let c = colEnds.findIndex((end) => end <= start)
      if (c === -1) { c = colEnds.length; colEnds.push(minutesOfDay(r.endTime)) }
      else colEnds[c] = minutesOfDay(r.endTime)
      return c
    })
    const nCols = colEnds.length
    cluster.forEach((r, k) => {
      const start = minutesOfDay(r.startTime)
      const end = minutesOfDay(r.endTime)
      blocks.push({
        r,
        top: ((start - startHour * 60) / 60) * HOUR_PX,
        height: Math.max(((end - start) / 60) * HOUR_PX, 24),
        col: cols[k],
        cols: nCols,
      })
    })
    i = j + 1
  }
  return blocks
}

interface Props {
  reservations: ReservationResponse[]
  /** Día a mostrar (YYYY-MM-DD, local). */
  date: string
  staffFilter: string
  onSelect: (r: ReservationResponse) => void
  onAddAt: () => void
}

/**
 * Vista de día tipo calendario: línea de tiempo con las reservas del día como bloques
 * posicionados por hora; clic en un hueco libre → nueva reserva ese día.
 */
export function AgendaDayTimeline({ reservations, date, staffFilter, onSelect, onAddAt }: Props) {
  const dayItems = useMemo(
    () => reservations.filter((r) =>
      localDayKey(r.startTime) === date && (staffFilter === 'all' || r.staffId === staffFilter)),
    [reservations, date, staffFilter],
  )

  const { startHour, endHour } = useMemo(() => {
    let start = DEFAULT_START, end = DEFAULT_END
    for (const r of dayItems) {
      start = Math.min(start, Math.floor(minutesOfDay(r.startTime) / 60))
      end = Math.max(end, Math.ceil(minutesOfDay(r.endTime) / 60))
    }
    return { startHour: Math.max(0, start), endHour: Math.min(24, Math.max(end, start + 1)) }
  }, [dayItems])

  const blocks = useMemo(() => layout(dayItems, startHour), [dayItems, startHour])
  const hours = Array.from({ length: endHour - startHour }, (_, i) => startHour + i)

  return (
    <div className="card !p-0 overflow-hidden" data-testid="agenda-day-timeline">
      {dayItems.length === 0 && (
        <p className="px-stack-md pt-stack-md text-sm text-on-surface-variant" data-testid="agenda-day-empty">
          No hay reservas este día. Pulsa en un hueco para añadir una.
        </p>
      )}
      <div className="relative flex">
        {/* Columna de horas */}
        <div className="w-12 shrink-0 border-r border-outline-variant/30">
          {hours.map((h) => (
            <div key={h} className="relative" style={{ height: HOUR_PX }}>
              <span className="absolute -top-2 right-1 text-[11px] font-medium text-on-surface-variant">{h}:00</span>
            </div>
          ))}
        </div>

        {/* Rejilla + bloques */}
        <div className="relative flex-1" style={{ height: (endHour - startHour) * HOUR_PX }}>
          {/* Líneas de hora (clic en el hueco → nueva reserva) */}
          {hours.map((h) => (
            <button
              key={h}
              type="button"
              onClick={onAddAt}
              data-testid="agenda-day-slot"
              className="absolute inset-x-0 border-t border-outline-variant/30 hover:bg-primary-container/10 transition-colors"
              style={{ top: (h - startHour) * HOUR_PX, height: HOUR_PX }}
              aria-label={`Añadir reserva a las ${h}:00`}
            />
          ))}

          {/* Bloques de reserva */}
          {blocks.map(({ r, top, height, col, cols }) => {
            const pending = r.status === 'pending'
            return (
              <button
                key={r.id}
                type="button"
                onClick={() => onSelect(r)}
                data-testid="agenda-day-block"
                className={`absolute overflow-hidden rounded-lg border px-2 py-1 text-left text-xs shadow-sm transition-shadow hover:shadow-md ${
                  pending
                    ? 'border-amber-400/60 bg-amber-100/80 text-amber-900'
                    : 'border-primary/30 bg-primary-container/70 text-on-surface'
                }`}
                style={{
                  top: top + 1,
                  height: height - 2,
                  left: `calc(${(col / cols) * 100}% + 4px)`,
                  width: `calc(${100 / cols}% - 8px)`,
                }}
              >
                <span className="block truncate font-bold">{r.clientName ?? 'Reserva'}</span>
                <span className="block truncate opacity-80">{hhmm(r.startTime)}–{hhmm(r.endTime)}{r.serviceName ? ` · ${r.serviceName}` : ''}</span>
                {r.staffName && height > 44 && <span className="block truncate opacity-70">{r.staffName}</span>}
              </button>
            )
          })}
        </div>
      </div>
    </div>
  )
}
