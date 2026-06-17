import { useEffect, useState } from 'react'
import { reservationService } from '../services/reservationService'
import { getApiError } from '../services/apiClient'
import { StatusPill } from '../components/StatusPill'
import type { ReservationResponse } from '../types/api'

function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString('es-ES', { dateStyle: 'medium', timeStyle: 'short' })
}

export function MyReservationsPage() {
  const [reservations, setReservations] = useState<ReservationResponse[] | null>(null)
  const [error, setError] = useState<string | null>(null)

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

  return (
    <section>
      <h1>Mis reservas</h1>
      <p className="text-on-surface-variant mb-stack-md">Tus próximas citas.</p>

      {error && (
        <p role="alert" className="alert" data-testid="my-reservations-error">
          {error}
        </p>
      )}

      {reservations === null && !error && <p className="text-on-surface-variant">Cargando…</p>}

      {reservations !== null && reservations.length === 0 && (
        <div className="card flex flex-col items-center text-center py-stack-xl" data-testid="my-reservations-empty">
          <span className="material-symbols-outlined text-[40px] text-on-surface-variant/50">event_busy</span>
          <p className="mt-stack-sm font-semibold">No tienes reservas todavía.</p>
          <p className="text-sm text-on-surface-variant">Cuando reserves, aparecerán aquí.</p>
        </div>
      )}

      {reservations !== null && reservations.length > 0 && (
        <ul className="flex flex-col gap-stack-sm" data-testid="my-reservations-list">
          {reservations.map((reservation) => (
            <li key={reservation.id} className="glass-card rounded-xl p-stack-md flex items-center gap-stack-md" data-testid="reservation-item">
              <span className="w-11 h-11 rounded-full bg-primary-container/15 text-primary flex items-center justify-center shrink-0">
                <span className="material-symbols-outlined">event</span>
              </span>
              <div className="flex-1 min-w-0">
                <p className="font-semibold">{formatDateTime(reservation.startTime)}</p>
              </div>
              <StatusPill status={reservation.status} />
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
