import { useEffect, useState } from 'react'
import { useAuth } from '../hooks/useAuth'
import { businessService } from '../services/businessService'
import { getApiError } from '../services/apiClient'
import type { DashboardResponse } from '../types/api'

function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString('es-ES', { dateStyle: 'medium', timeStyle: 'short' })
}

function formatEuro(amount: number): string {
  return new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR' }).format(amount)
}

export function DashboardPage() {
  const { businessId, isOwner } = useAuth()
  const [dashboard, setDashboard] = useState<DashboardResponse | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!businessId) return
    let active = true
    businessService
      .dashboard(businessId)
      .then((data) => {
        if (active) setDashboard(data)
      })
      .catch((err) => {
        if (active) setError(getApiError(err)?.message ?? 'No se pudo cargar el panel.')
      })
    return () => {
      active = false
    }
  }, [businessId])

  if (!isOwner || !businessId) {
    return (
      <section>
        <h1>Panel</h1>
        <p>Esta sección es solo para propietarios de un negocio.</p>
      </section>
    )
  }

  return (
    <section>
      <h1>Panel del negocio</h1>

      {error && (
        <p role="alert" data-testid="dashboard-error">
          {error}
        </p>
      )}

      {dashboard === null && !error && <p>Cargando…</p>}

      {dashboard !== null && (
        <>
          <div data-testid="dashboard-metrics" style={{ display: 'flex', gap: '1.5rem', flexWrap: 'wrap' }}>
            <article data-testid="metric-total-reservations">
              <h2>{dashboard.totalReservations}</h2>
              <p>Reservas totales</p>
            </article>
            <article data-testid="metric-reservations-month">
              <h2>{dashboard.reservationsThisMonth}</h2>
              <p>Reservas este mes</p>
            </article>
            <article data-testid="metric-monthly-revenue">
              <h2>{formatEuro(dashboard.estimatedMonthlyRevenue)}</h2>
              <p>Ingresos estimados (mes)</p>
            </article>
          </div>

          <h2>Próximas reservas</h2>
          {dashboard.upcomingReservations.length === 0 ? (
            <p data-testid="dashboard-upcoming-empty">No hay próximas reservas.</p>
          ) : (
            <ul data-testid="dashboard-upcoming-list">
              {dashboard.upcomingReservations.map((reservation) => (
                <li key={reservation.id} data-testid="dashboard-upcoming-item">
                  {formatDateTime(reservation.startTime)} — {reservation.status}
                </li>
              ))}
            </ul>
          )}
        </>
      )}
    </section>
  )
}
