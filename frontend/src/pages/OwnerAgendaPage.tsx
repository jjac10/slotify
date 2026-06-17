import { useEffect, useState } from 'react'
import { useAuth } from '../hooks/useAuth'
import { reservationService } from '../services/reservationService'
import { getApiError } from '../services/apiClient'
import { StatusPill } from '../components/StatusPill'
import type { ReservationResponse } from '../types/api'

function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString('es-ES', {
    dateStyle: 'medium',
    timeStyle: 'short',
  })
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
        <p>Esta sección es solo para propietarios de un negocio.</p>
      </section>
    )
  }

  return (
    <section>
      <h1>Agenda del negocio</h1>

      {error && (
        <p role="alert" data-testid="agenda-error">
          {error}
        </p>
      )}

      {reservations === null && !error && <p>Cargando…</p>}

      {reservations !== null && reservations.length === 0 && (
        <p data-testid="agenda-empty">No hay reservas en la agenda todavía.</p>
      )}

      {reservations !== null && reservations.length > 0 && (
        <ul className="list-plain" data-testid="agenda-list">
          {reservations.map((reservation) => (
            <li
              key={reservation.id}
              className="card"
              data-testid="agenda-item"
              style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}
            >
              <span>{formatDateTime(reservation.startTime)}</span>
              <StatusPill status={reservation.status} />
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
