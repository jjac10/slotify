import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { useAuth } from '../hooks/useAuth'
import { businessService } from '../services/businessService'
import { getApiError } from '../services/apiClient'
import type { BusinessHour } from '../types/api'

/** Días en orden de visualización (Lunes primero). dayOfWeek: 0=domingo … 6=sábado. */
const DAYS: ReadonlyArray<{ dayOfWeek: number; label: string }> = [
  { dayOfWeek: 1, label: 'Lunes' },
  { dayOfWeek: 2, label: 'Martes' },
  { dayOfWeek: 3, label: 'Miércoles' },
  { dayOfWeek: 4, label: 'Jueves' },
  { dayOfWeek: 5, label: 'Viernes' },
  { dayOfWeek: 6, label: 'Sábado' },
  { dayOfWeek: 0, label: 'Domingo' },
]

interface DayRow {
  dayOfWeek: number
  label: string
  isClosed: boolean
  opening: string // "HH:mm"
  closing: string // "HH:mm"
}

/** "09:00:00" → "09:00"; null → fallback. */
function toInput(time: string | null, fallback: string): string {
  return time ? time.slice(0, 5) : fallback
}

/** Construye las filas: refleja el horario guardado o, si no hay, prefija L–V 09–17. */
function buildRows(saved: BusinessHour[]): DayRow[] {
  const byDay = new Map(saved.map((h) => [h.dayOfWeek, h]))
  return DAYS.map(({ dayOfWeek, label }) => {
    const h = byDay.get(dayOfWeek)
    if (h) {
      return {
        dayOfWeek,
        label,
        isClosed: h.isClosed,
        opening: toInput(h.openingTime, '09:00'),
        closing: toInput(h.closingTime, '17:00'),
      }
    }
    // Sin datos guardados: por defecto L–V abierto 09–17, fin de semana cerrado.
    const weekday = dayOfWeek >= 1 && dayOfWeek <= 5
    return { dayOfWeek, label, isClosed: !weekday, opening: '09:00', closing: '17:00' }
  })
}

export function BusinessHoursPage() {
  const { businessId, isOwner } = useAuth()
  const [rows, setRows] = useState<DayRow[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const [saved, setSaved] = useState(false)

  useEffect(() => {
    if (!businessId) return
    let active = true
    businessService
      .getHours(businessId)
      .then((hours) => {
        if (active) setRows(buildRows(hours))
      })
      .catch((err) => {
        if (active) setError(getApiError(err)?.message ?? 'No se pudo cargar el horario.')
      })
    return () => {
      active = false
    }
  }, [businessId])

  function updateRow(dayOfWeek: number, patch: Partial<DayRow>) {
    setRows((prev) => prev?.map((r) => (r.dayOfWeek === dayOfWeek ? { ...r, ...patch } : r)) ?? prev)
    setSaved(false)
  }

  async function handleSave(e: FormEvent) {
    e.preventDefault()
    if (!businessId || !rows) return
    // Validación local: en días abiertos, apertura < cierre.
    const invalid = rows.find((r) => !r.isClosed && r.opening >= r.closing)
    if (invalid) {
      setError(`En ${invalid.label} la apertura debe ser anterior al cierre.`)
      return
    }
    setError(null)
    setSaving(true)
    try {
      const days: BusinessHour[] = rows.map((r) => ({
        dayOfWeek: r.dayOfWeek,
        isClosed: r.isClosed,
        openingTime: r.isClosed ? null : `${r.opening}:00`,
        closingTime: r.isClosed ? null : `${r.closing}:00`,
      }))
      const updated = await businessService.setHours(businessId, days)
      setRows(buildRows(updated))
      setSaved(true)
    } catch (err) {
      setError(getApiError(err)?.message ?? 'No se pudo guardar el horario.')
    } finally {
      setSaving(false)
    }
  }

  if (!isOwner || !businessId) {
    return (
      <section>
        <h1>Horario</h1>
        <p className="text-on-surface-variant">Esta sección es solo para propietarios de un negocio.</p>
      </section>
    )
  }

  return (
    <section>
      <h1>Horario del negocio</h1>
      <p className="text-on-surface-variant mb-stack-md">
        Define cuándo abres cada día. Los clientes solo verán huecos dentro de este horario.
      </p>

      {error && (
        <p role="alert" className="alert" data-testid="hours-error">
          {error}
        </p>
      )}
      {rows === null && !error && <p className="text-on-surface-variant">Cargando…</p>}

      {rows !== null && (
        <form onSubmit={handleSave} data-testid="hours-form" className="max-w-lg">
          <ul className="flex flex-col gap-stack-sm mb-stack-md">
            {rows.map((r) => (
              <li
                key={r.dayOfWeek}
                className="glass-card rounded-xl px-stack-md py-3 flex items-center gap-stack-md flex-wrap"
                data-testid={`hours-day-${r.dayOfWeek}`}
              >
                <span className="font-semibold w-24">{r.label}</span>
                <label className="inline-flex items-center gap-2 text-sm font-medium cursor-pointer">
                  <input
                    type="checkbox"
                    className="w-4 h-4 accent-primary-container"
                    data-testid={`hours-day-${r.dayOfWeek}-open-toggle`}
                    checked={!r.isClosed}
                    onChange={(e) => updateRow(r.dayOfWeek, { isClosed: !e.target.checked })}
                  />
                  {r.isClosed ? 'Cerrado' : 'Abierto'}
                </label>
                {!r.isClosed && (
                  <span className="flex items-center gap-2 ml-auto">
                    <input
                      type="time"
                      className="field-input !py-1.5 w-28"
                      data-testid={`hours-day-${r.dayOfWeek}-opening`}
                      value={r.opening}
                      onChange={(e) => updateRow(r.dayOfWeek, { opening: e.target.value })}
                      required
                    />
                    <span className="text-on-surface-variant">–</span>
                    <input
                      type="time"
                      className="field-input !py-1.5 w-28"
                      data-testid={`hours-day-${r.dayOfWeek}-closing`}
                      value={r.closing}
                      onChange={(e) => updateRow(r.dayOfWeek, { closing: e.target.value })}
                      required
                    />
                  </span>
                )}
              </li>
            ))}
          </ul>

          <div className="flex items-center gap-stack-md">
            <button type="submit" className="btn-primary" data-testid="hours-save" disabled={saving}>
              {saving ? 'Guardando…' : 'Guardar horario'}
            </button>
            {saved && (
              <p className="inline-flex items-center gap-1 text-sm font-semibold text-secondary" data-testid="hours-saved">
                <span className="material-symbols-outlined text-[18px]">check_circle</span>
                Horario guardado.
              </p>
            )}
          </div>
        </form>
      )}
    </section>
  )
}
