import { useEffect, useState } from 'react'
import { useAuth } from '../hooks/useAuth'
import { reservationService } from '../services/reservationService'
import { getApiError } from '../services/apiClient'
import { StatusPill } from '../components/StatusPill'
import type { ReservationResponse } from '../types/api'

function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString('es-ES', { dateStyle: 'medium', timeStyle: 'short' })
}

export function OwnerAgendaPage() {
  const { businessId, isOwner } = useAuth()
  const [reservations, setReservations] = useState<ReservationResponse[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!businessId) return
    let active = true
    reservationService
      .listForBusiness(businessId)
      .then((data) => {
        if (active) setReservations(data)
      })
      .catch((err) => {
        if (active) setError(getApiError(err)?.message ?? 'No se pudo cargar la agenda.')
      })
    return () => {
      active = false
    }
  }, [businessId])

  if (!isOwner || !businessId) {
    return (
      <section>
        <h1>Agenda</h1>
        <p className="text-on-surface-variant">Esta sección es solo para propietarios de un negocio.</p>
      </section>
    )
  }

  return (
    <section>
      <h1>Agenda del negocio</h1>
      <p className="text-on-surface-variant mb-stack-md">Todas las reservas de tu negocio.</p>

      {error && (
        <p role="alert" className="alert" data-testid="agenda-error">
          {error}
        </p>
      )}

      {reservations === null && !error && <p className="text-on-surface-variant">Cargando…</p>}

      {reservations !== null && reservations.length === 0 && (
        <div className="card flex flex-col items-center text-center py-stack-xl" data-testid="agenda-empty">
          <span className="material-symbols-outlined text-[40px] text-on-surface-variant/50">calendar_today</span>
          <p className="mt-stack-sm font-semibold">No hay reservas en la agenda todavía.</p>
        </div>
      )}

      {reservations !== null && reservations.length > 0 && (
        <ul className="flex flex-col gap-stack-sm" data-testid="agenda-list">
          {reservations.map((reservation) => (
            <li key={reservation.id} className="glass-card rounded-xl p-stack-md flex items-center gap-stack-md" data-testid="agenda-item">
              <span className="w-11 h-11 rounded-full bg-secondary-container/40 text-on-secondary-container flex items-center justify-center shrink-0">
                <span className="material-symbols-outlined">person</span>
              </span>
              <div className="flex-1 min-w-0">
                <p className="truncate font-semibold">
                  {reservation.serviceName ?? 'Reserva'}
                  {reservation.staffName ? ` · ${reservation.staffName}` : ''}
                </p>
                <p className="truncate text-sm text-on-surface-variant">
                  {formatDateTime(reservation.startTime)} · {reservation.guestId ? 'Invitado' : 'Cliente'}
                </p>
              </div>
              <StatusPill status={reservation.status} />
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
