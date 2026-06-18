import { useEffect, useMemo, useState } from 'react'
import { reservationService } from '../services/reservationService'
import { getApiError } from '../services/apiClient'
import { StatusPill } from '../components/StatusPill'
import type { ReservationResponse } from '../types/api'

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('es-ES', { day: 'numeric', month: 'long', year: 'numeric' })
}
function formatTime(iso: string): string {
  return new Date(iso).toLocaleTimeString('es-ES', { hour: '2-digit', minute: '2-digit' })
}

type Tab = 'upcoming' | 'past'

export function MyReservationsPage() {
  const [reservations, setReservations] = useState<ReservationResponse[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [tab, setTab] = useState<Tab>('upcoming')

  useEffect(() => {
    let active = true
    reservationService
      .listMine()
      .then((data) => {
        if (active) setReservations(data)
      })
      .catch((err) => {
        if (active) setError(getApiError(err)?.message ?? 'No se pudieron cargar tus reservas.')
      })
    return () => {
      active = false
    }
  }, [])

  const visible = useMemo(() => {
    if (!reservations) return null
    const now = Date.now()
    return reservations.filter((r) =>
      tab === 'upcoming' ? new Date(r.startTime).getTime() >= now : new Date(r.startTime).getTime() < now,
    )
  }, [reservations, tab])

  return (
    <section>
      <h1>Mis reservas</h1>
      <p className="text-on-surface-variant mb-stack-md">Gestiona tus citas e historial.</p>

      {/* Tabs */}
      <div className="mb-stack-md flex items-center gap-stack-sm">
        {(['upcoming', 'past'] as const).map((t) => (
          <button
            key={t}
            type="button"
            onClick={() => setTab(t)}
            className={`rounded-full px-5 py-2 text-sm font-bold transition-colors ${
              tab === t ? 'bg-primary-container text-on-primary shadow-card' : 'text-on-surface-variant hover:bg-surface-container-low'
            }`}
          >
            {t === 'upcoming' ? 'Próximas' : 'Pasadas'}
          </button>
        ))}
      </div>

      {error && (
        <p role="alert" className="alert" data-testid="my-reservations-error">
          {error}
        </p>
      )}

      {visible === null && !error && <p className="text-on-surface-variant">Cargando…</p>}

      {visible !== null && visible.length === 0 && (
        <div className="card flex flex-col items-center text-center py-stack-xl" data-testid="my-reservations-empty">
          <span className="material-symbols-outlined text-[40px] text-on-surface-variant/40">
            {tab === 'upcoming' ? 'event_busy' : 'history'}
          </span>
          <p className="mt-stack-sm font-semibold">
            {tab === 'upcoming' ? 'No tienes reservas próximas.' : 'No tienes reservas pasadas.'}
          </p>
          {tab === 'upcoming' && <p className="text-sm text-on-surface-variant">Cuando reserves, aparecerán aquí.</p>}
        </div>
      )}

      {visible !== null && visible.length > 0 && (
        <ul className="flex flex-col gap-stack-sm" data-testid="my-reservations-list">
          {visible.map((r) => (
            <li key={r.id} className="card flex items-center gap-stack-md" data-testid="reservation-item">
              <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-primary-container/15 text-primary">
                <span className="material-symbols-outlined">event</span>
              </span>
              <div className="min-w-0 flex-1">
                <p className="truncate font-bold leading-tight">{r.businessName ?? 'Reserva'}</p>
                <p className="truncate text-sm text-on-surface-variant">
                  {r.serviceName ? `${r.serviceName} · ` : ''}
                  {formatDate(r.startTime)} · {formatTime(r.startTime)}
                </p>
              </div>
              <StatusPill status={r.status} />
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
