import { useEffect, useState } from 'react'
import { reservationService } from '../services/reservationService'
import { getApiError } from '../services/apiClient'
import type { ReservationResponse } from '../types/api'

function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString('es-ES', {
    dateStyle: 'medium',
    timeStyle: 'short',
  })
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

      {error && (
        <p role="alert" data-testid="my-reservations-error">
          {error}
        </p>
      )}

      {reservations === null && !error && <p>Cargando…</p>}

      {reservations !== null && reservations.length === 0 && (
        <p data-testid="my-reservations-empty">No tienes reservas todavía.</p>
      )}

      {reservations !== null && reservations.length > 0 && (
        <ul data-testid="my-reservations-list">
          {reservations.map((reservation) => (
            <li key={reservation.id} data-testid="reservation-item">
              {formatDateTime(reservation.startTime)} — {reservation.status}
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
