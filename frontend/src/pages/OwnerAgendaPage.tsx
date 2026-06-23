import { useEffect, useMemo, useState } from 'react'
import { useAuth } from '../hooks/useAuth'
import { reservationService } from '../services/reservationService'
import { getApiError } from '../services/apiClient'
import { StatusPill } from '../components/StatusPill'
import { RescheduleModal } from '../components/RescheduleModal'
import { NewReservationModal } from '../components/NewReservationModal'
import { AgendaDayTimeline } from '../components/AgendaDayTimeline'
import type { ReservationResponse } from '../types/api'

const DAY_MS = 86_400_000

/** "YYYY-MM-DD" local de una fecha. */
function isoDate(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

function startOfDay(d: Date): number {
  const x = new Date(d)
  x.setHours(0, 0, 0, 0)
  return x.getTime()
}

/** Clave estable del día (para agrupar). */
function dayKey(iso: string): string {
  const d = new Date(iso)
  return `${d.getFullYear()}-${d.getMonth()}-${d.getDate()}`
}

/** Cabecera del día: Hoy / Mañana / Ayer o la fecha larga. */
function dayLabel(iso: string): string {
  const diff = Math.round((startOfDay(new Date(iso)) - startOfDay(new Date())) / DAY_MS)
  if (diff === 0) return 'Hoy'
  if (diff === 1) return 'Mañana'
  if (diff === -1) return 'Ayer'
  return new Date(iso).toLocaleDateString('es-ES', { weekday: 'long', day: 'numeric', month: 'long' })
}

function timeLabel(iso: string): string {
  return new Date(iso).toLocaleTimeString('es-ES', { hour: '2-digit', minute: '2-digit' })
}

/** Lunes (00:00) de la semana de una fecha. */
function startOfWeek(d: Date): Date {
  const x = new Date(d)
  x.setHours(0, 0, 0, 0)
  x.setDate(x.getDate() - ((x.getDay() + 6) % 7)) // getDay: 0=domingo → desplazamos a lunes
  return x
}

function weekKey(iso: string): string {
  const s = startOfWeek(new Date(iso))
  return `w-${s.getFullYear()}-${s.getMonth()}-${s.getDate()}`
}

/** Cabecera de semana: "Esta semana · 23 – 29 jun" (o el rango con su mes). */
function weekLabel(iso: string): string {
  const s = startOfWeek(new Date(iso))
  const e = new Date(s)
  e.setDate(e.getDate() + 6)
  const prefix = s.getTime() === startOfWeek(new Date()).getTime() ? 'Esta semana · ' : ''
  const sameMonth = s.getMonth() === e.getMonth()
  const start = s.toLocaleDateString('es-ES', sameMonth ? { day: 'numeric' } : { day: 'numeric', month: 'short' })
  const end = e.toLocaleDateString('es-ES', { day: 'numeric', month: 'short' })
  return `${prefix}${start} – ${end}`
}

interface ItemProps {
  reservation: ReservationResponse
  onCancelled: (id: string) => void
  onConfirmed: (updated: ReservationResponse) => void
  onReschedule: () => void
}

function AgendaItem({ reservation: r, onCancelled, onConfirmed, onReschedule }: ItemProps) {
  const isActive = r.status === 'pending' || r.status === 'confirmed'
  const canAct = isActive && new Date(r.startTime).getTime() > Date.now()
  const [confirmingCancel, setConfirmingCancel] = useState(false)
  const [cancelling, setCancelling] = useState(false)
  const [confirming, setConfirming] = useState(false)
  const [cancelError, setCancelError] = useState<string | null>(null)

  async function handleCancel() {
    setCancelling(true)
    try {
      await reservationService.cancel(r.id)
      onCancelled(r.id)
    } catch (err) {
      setCancelError(getApiError(err)?.message ?? 'No se pudo cancelar.')
      setCancelling(false)
    }
  }

  async function handleConfirm() {
    setConfirming(true)
    try {
      const updated = await reservationService.confirm(r.id)
      onConfirmed(updated)
    } catch (err) {
      setCancelError(getApiError(err)?.message ?? 'No se pudo confirmar.')
      setConfirming(false)
    }
  }

  return (
    <li className="glass-card rounded-xl p-stack-md flex flex-col gap-stack-sm" data-testid="agenda-item">
      <div className="flex items-center gap-stack-md">
        <span className="w-11 h-11 rounded-full bg-secondary-container/40 text-on-secondary-container flex items-center justify-center shrink-0">
          <span className="material-symbols-outlined">person</span>
        </span>
        <div className="flex-1 min-w-0">
          <p className="truncate font-semibold">
            {r.clientName ?? 'Sin nombre'}
            <span className={`ml-2 align-middle inline-block rounded-full px-1.5 py-0.5 text-[10px] font-bold ${
              r.guestId ? 'bg-surface-container text-on-surface-variant' : 'bg-primary-container/30 text-primary'
            }`}>
              {r.guestId ? 'Invitado' : 'Cliente'}
            </span>
          </p>
          <p className="truncate text-sm text-on-surface-variant">
            {r.serviceName ?? 'Reserva'}{r.staffName ? ` · ${r.staffName}` : ''}
          </p>
          <p className="truncate text-xs text-on-surface-variant">
            {timeLabel(r.startTime)} – {timeLabel(r.endTime)}
          </p>
        </div>
        <StatusPill status={r.status} />
      </div>

      {canAct && !confirmingCancel && (
        <div className="flex gap-1 pt-1 border-t border-outline-variant/30">
          {r.status === 'pending' && (
            <button
              type="button"
              onClick={handleConfirm}
              disabled={confirming}
              className="flex items-center gap-1 rounded-lg px-3 py-1.5 text-xs font-semibold text-emerald-700 hover:bg-emerald-100 disabled:opacity-50 transition-colors"
              data-testid="confirm-btn"
            >
              <span className="material-symbols-outlined text-[16px]">check_circle</span>
              {confirming ? 'Confirmando…' : 'Confirmar'}
            </button>
          )}
          <button
            type="button"
            onClick={onReschedule}
            className="flex items-center gap-1 rounded-lg px-3 py-1.5 text-xs font-semibold text-primary hover:bg-primary-container/15 transition-colors"
            data-testid="reschedule-btn"
          >
            <span className="material-symbols-outlined text-[16px]">edit_calendar</span>
            Reprogramar
          </button>
          <button
            type="button"
            onClick={() => setConfirmingCancel(true)}
            className="flex items-center gap-1 rounded-lg px-3 py-1.5 text-xs font-semibold text-error hover:bg-error-container/30 transition-colors"
            data-testid="cancel-btn"
          >
            <span className="material-symbols-outlined text-[16px]">cancel</span>
            Cancelar
          </button>
        </div>
      )}

      {confirmingCancel && (
        <div className="flex flex-col gap-stack-sm pt-1 border-t border-outline-variant/30">
          <p className="text-sm font-medium">¿Cancelar esta reserva?</p>
          {cancelError && <p role="alert" className="alert text-xs">{cancelError}</p>}
          <div className="flex gap-2">
            <button
              type="button"
              onClick={handleCancel}
              disabled={cancelling}
              className="rounded-lg px-3 py-1.5 text-xs font-semibold bg-error text-on-error hover:brightness-105 disabled:opacity-50 transition-colors"
              data-testid="cancel-confirm-btn"
            >
              {cancelling ? 'Cancelando…' : 'Sí, cancelar'}
            </button>
            <button
              type="button"
              onClick={() => { setConfirmingCancel(false); setCancelError(null) }}
              disabled={cancelling}
              className="rounded-lg px-3 py-1.5 text-xs font-semibold text-on-surface-variant hover:bg-surface-container-low disabled:opacity-50 transition-colors"
            >
              Volver
            </button>
          </div>
        </div>
      )}
    </li>
  )
}

export function OwnerAgendaPage() {
  const { businessId, isOwner } = useAuth()
  const [reservations, setReservations] = useState<ReservationResponse[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [rescheduleTarget, setRescheduleTarget] = useState<ReservationResponse | null>(null)
  const [creating, setCreating] = useState(false)
  const [tab, setTab] = useState<'upcoming' | 'past'>('upcoming')
  const [staffFilter, setStaffFilter] = useState('all')
  const [search, setSearch] = useState('')
  const [grouping, setGrouping] = useState<'day' | 'week'>('day')
  const [view, setView] = useState<'list' | 'day'>('list')
  const [dayDate, setDayDate] = useState(() => isoDate(new Date()))

  function loadReservations() {
    if (!businessId) return
    reservationService
      .listForBusiness(businessId)
      .then(setReservations)
      .catch((err) => setError(getApiError(err)?.message ?? 'No se pudo cargar la agenda.'))
  }

  useEffect(() => {
    if (!businessId) return
    let active = true
    reservationService
      .listForBusiness(businessId)
      .then((data) => { if (active) setReservations(data) })
      .catch((err) => { if (active) setError(getApiError(err)?.message ?? 'No se pudo cargar la agenda.') })
    return () => { active = false }
  }, [businessId])

  // Trabajadores presentes en la agenda (para el filtro), derivados de las reservas.
  const staffOptions = useMemo(() => {
    const byId = new Map<string, string>()
    for (const r of reservations ?? []) if (r.staffId) byId.set(r.staffId, r.staffName ?? 'Trabajador')
    return [...byId.entries()].map(([id, name]) => ({ id, name }))
  }, [reservations])

  // Resumen: reservas de hoy y de los próximos 7 días (solo futuras).
  const summary = useMemo(() => {
    const now = Date.now()
    const today = startOfDay(new Date())
    let todayCount = 0, weekCount = 0
    for (const r of reservations ?? []) {
      if (new Date(r.startTime).getTime() < now) continue
      const day = startOfDay(new Date(r.startTime))
      if (day === today) todayCount++
      if (day < today + 7 * DAY_MS) weekCount++
    }
    return { todayCount, weekCount }
  }, [reservations])

  // Reservas filtradas (pestaña + trabajador + búsqueda), ordenadas y agrupadas por día.
  const groups = useMemo(() => {
    if (!reservations) return null
    const now = Date.now()
    const q = search.trim().toLowerCase()
    const filtered = reservations
      .filter((r) => {
        const isPast = new Date(r.startTime).getTime() < now
        if (tab === 'upcoming' ? isPast : !isPast) return false
        if (staffFilter !== 'all' && r.staffId !== staffFilter) return false
        if (q && !(r.clientName ?? '').toLowerCase().includes(q)) return false
        return true
      })
      .sort((a, b) => {
        const ta = new Date(a.startTime).getTime(), tb = new Date(b.startTime).getTime()
        return tab === 'upcoming' ? ta - tb : tb - ta // próximas: antes primero; pasadas: recientes primero
      })

    // Agrupado por día (Hoy/Mañana/fecha) o por semana (rango lunes–domingo).
    const keyOf = grouping === 'week' ? weekKey : dayKey
    const labelOf = grouping === 'week' ? weekLabel : dayLabel
    const result: { key: string; label: string; items: ReservationResponse[] }[] = []
    for (const r of filtered) {
      const key = keyOf(r.startTime)
      const last = result[result.length - 1]
      if (last && last.key === key) last.items.push(r)
      else result.push({ key, label: labelOf(r.startTime), items: [r] })
    }
    return result
  }, [reservations, tab, staffFilter, search, grouping])

  if (!isOwner || !businessId) {
    return (
      <section>
        <h1>Agenda</h1>
        <p className="text-on-surface-variant">Esta sección es solo para propietarios de un negocio.</p>
      </section>
    )
  }

  function handleCancelled(id: string) {
    setReservations((prev) => prev?.filter((r) => r.id !== id) ?? null)
  }

  function handleConfirmed(updated: ReservationResponse) {
    setReservations((prev) =>
      prev?.map((r) => (r.id === updated.id ? { ...r, status: updated.status } : r)) ?? null,
    )
  }

  function handleRescheduled(updated: ReservationResponse) {
    setReservations((prev) =>
      prev?.map((r) =>
        r.id === updated.id
          ? { ...r, startTime: updated.startTime, endTime: updated.endTime, status: updated.status }
          : r,
      ) ?? null,
    )
    setRescheduleTarget(null)
  }

  function shiftDay(delta: number) {
    const d = new Date(`${dayDate}T12:00:00`)
    d.setDate(d.getDate() + delta)
    setDayDate(isoDate(d))
  }

  return (
    <section>
      <div className="flex items-start justify-between gap-stack-md mb-stack-md">
        <div>
          <h1>Agenda del negocio</h1>
          <p className="text-on-surface-variant">
            Hoy <strong className="text-on-surface">{summary.todayCount}</strong> · esta semana <strong className="text-on-surface">{summary.weekCount}</strong>
          </p>
        </div>
        <button type="button" onClick={() => setCreating(true)} className="btn-primary shrink-0 inline-flex items-center gap-1" data-testid="new-reservation-btn">
          <span className="material-symbols-outlined text-[18px]">add</span>
          Nueva reserva
        </button>
      </div>

      {error && (
        <p role="alert" className="alert" data-testid="agenda-error">
          {error}
        </p>
      )}

      {/* Toggle Lista / Día + filtro por trabajador (compartido) */}
      <div className="mb-stack-md flex items-center justify-between gap-stack-sm flex-wrap">
        <div className="inline-flex gap-1 rounded-full bg-surface-container p-1" data-testid="agenda-view-toggle">
          {([['list', 'Lista', 'view_agenda'], ['day', 'Día', 'calendar_view_day']] as const).map(([v, label, icon]) => (
            <button
              key={v}
              type="button"
              onClick={() => setView(v)}
              data-testid={`agenda-view-${v}`}
              className={`inline-flex items-center gap-1 rounded-full px-3 py-1.5 text-sm font-bold transition-all ${
                view === v ? 'bg-primary text-on-primary shadow-sm' : 'text-on-surface-variant hover:text-on-surface'
              }`}
            >
              <span className="material-symbols-outlined text-[18px]">{icon}</span>
              {label}
            </button>
          ))}
        </div>
        {staffOptions.length > 1 && (
          <select
            data-testid="agenda-staff-filter"
            className="field-input shrink-0"
            value={staffFilter}
            onChange={(e) => setStaffFilter(e.target.value)}
            aria-label="Filtrar por trabajador"
          >
            <option value="all">Todo el equipo</option>
            {staffOptions.map((s) => (
              <option key={s.id} value={s.id}>{s.name}</option>
            ))}
          </select>
        )}
      </div>

      {reservations === null && !error && <p className="text-on-surface-variant">Cargando…</p>}

      {view === 'day' ? (
        <>
          {/* Navegación de día */}
          <div className="mb-stack-md flex items-center justify-center gap-stack-md">
            <button type="button" onClick={() => shiftDay(-1)} data-testid="agenda-day-prev"
              className="flex h-9 w-9 items-center justify-center rounded-full hover:bg-surface-container-low" aria-label="Día anterior">
              <span className="material-symbols-outlined">chevron_left</span>
            </button>
            <button type="button" onClick={() => setDayDate(isoDate(new Date()))} data-testid="agenda-day-today"
              className="min-w-[10rem] text-center text-sm font-bold capitalize hover:text-primary">
              {dayLabel(`${dayDate}T12:00:00`)}
            </button>
            <button type="button" onClick={() => shiftDay(1)} data-testid="agenda-day-next"
              className="flex h-9 w-9 items-center justify-center rounded-full hover:bg-surface-container-low" aria-label="Día siguiente">
              <span className="material-symbols-outlined">chevron_right</span>
            </button>
          </div>
          {reservations !== null && (
            <AgendaDayTimeline
              reservations={reservations}
              date={dayDate}
              staffFilter={staffFilter}
              onSelect={(r) => setRescheduleTarget(r)}
              onAddAt={() => setCreating(true)}
            />
          )}
        </>
      ) : (
        <>
          {/* Pestañas Próximas / Pasadas + agrupado Día / Semana */}
          <div className="mb-stack-sm flex flex-wrap items-center justify-between gap-stack-sm">
            <div className="inline-flex gap-1 rounded-full bg-surface-container p-1" data-testid="agenda-tabs">
              {([['upcoming', 'Próximas'], ['past', 'Pasadas']] as const).map(([value, label]) => (
                <button
                  key={value}
                  type="button"
                  onClick={() => setTab(value)}
                  data-testid={`agenda-tab-${value}`}
                  className={`rounded-full px-4 py-1.5 text-sm font-bold transition-all ${
                    tab === value ? 'bg-primary text-on-primary shadow-sm' : 'text-on-surface-variant hover:text-on-surface'
                  }`}
                >
                  {label}
                </button>
              ))}
            </div>
            <div className="inline-flex gap-1 rounded-full bg-surface-container p-1" data-testid="agenda-grouping">
              {([['day', 'Día'], ['week', 'Semana']] as const).map(([value, label]) => (
                <button
                  key={value}
                  type="button"
                  onClick={() => setGrouping(value)}
                  data-testid={`agenda-group-${value}`}
                  className={`rounded-full px-4 py-1.5 text-sm font-bold transition-all ${
                    grouping === value ? 'bg-primary text-on-primary shadow-sm' : 'text-on-surface-variant hover:text-on-surface'
                  }`}
                >
                  {label}
                </button>
              ))}
            </div>
          </div>

          {/* Buscar cliente */}
          <div className="mb-stack-md relative">
            <span className="material-symbols-outlined pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-on-surface-variant">search</span>
            <input
              type="search"
              data-testid="agenda-search"
              className="field-input w-full !pl-11"
              placeholder="Buscar cliente…"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              aria-label="Buscar por nombre de cliente"
            />
          </div>

          {groups !== null && groups.length === 0 && (
            <div className="card flex flex-col items-center text-center py-stack-xl" data-testid="agenda-empty">
              <span className="material-symbols-outlined text-[40px] text-on-surface-variant/50">calendar_today</span>
              <p className="mt-stack-sm font-semibold">
                {search.trim() || staffFilter !== 'all'
                  ? 'No hay reservas que coincidan con el filtro.'
                  : tab === 'upcoming' ? 'No hay reservas próximas.' : 'No hay reservas pasadas.'}
              </p>
            </div>
          )}

          {groups !== null && groups.length > 0 && (
            <div className="flex flex-col gap-stack-md" data-testid="agenda-list">
              {groups.map((g) => (
                <div key={g.key}>
                  <div className="mb-stack-sm flex items-center gap-2 text-xs font-bold uppercase tracking-wide text-on-surface-variant" data-testid="agenda-day-header">
                    <span className="material-symbols-outlined text-[16px]">event</span>
                    <span className="capitalize">{g.label}</span>
                    <span className="font-semibold normal-case opacity-70">· {g.items.length}</span>
                  </div>
                  <ul className="flex flex-col gap-stack-sm">
                    {g.items.map((r) => (
                      <AgendaItem
                        key={r.id}
                        reservation={r}
                        onCancelled={handleCancelled}
                        onConfirmed={handleConfirmed}
                        onReschedule={() => setRescheduleTarget(r)}
                      />
                    ))}
                  </ul>
                </div>
              ))}
            </div>
          )}
        </>
      )}

      {rescheduleTarget && (
        <RescheduleModal
          reservation={rescheduleTarget}
          onClose={() => setRescheduleTarget(null)}
          onRescheduled={handleRescheduled}
        />
      )}

      {creating && (
        <NewReservationModal
          businessId={businessId}
          initialDate={view === 'day' ? dayDate : undefined}
          onClose={() => setCreating(false)}
          onCreated={() => { setCreating(false); loadReservations() }}
        />
      )}
    </section>
  )
}
