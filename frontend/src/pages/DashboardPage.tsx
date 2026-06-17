import { useEffect, useState } from 'react'
import { useAuth } from '../hooks/useAuth'
import { businessService } from '../services/businessService'
import { getApiError } from '../services/apiClient'
import { StatusPill } from '../components/StatusPill'
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
          <div className="metric-grid" data-testid="dashboard-metrics">
            <div className="metric" data-testid="metric-total-reservations">
              <p className="metric-value">{dashboard.totalReservations}</p>
              <p className="metric-label">Reservas totales</p>
            </div>
            <div className="metric" data-testid="metric-reservations-month">
              <p className="metric-value">{dashboard.reservationsThisMonth}</p>
              <p className="metric-label">Reservas este mes</p>
            </div>
            <div className="metric" data-testid="metric-monthly-revenue">
              <p className="metric-value">{formatEuro(dashboard.estimatedMonthlyRevenue)}</p>
              <p className="metric-label">Ingresos estimados (mes)</p>
            </div>
          </div>

          <h2>Próximas reservas</h2>
          {dashboard.upcomingReservations.length === 0 ? (
            <p data-testid="dashboard-upcoming-empty">No hay próximas reservas.</p>
          ) : (
            <ul className="list-plain" data-testid="dashboard-upcoming-list">
              {dashboard.upcomingReservations.map((reservation) => (
                <li
                  key={reservation.id}
                  className="card"
                  data-testid="dashboard-upcoming-item"
                  style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}
                >
                  <span>{formatDateTime(reservation.startTime)}</span>
                  <StatusPill status={reservation.status} />
                </li>
              ))}
            </ul>
          )}
        </>
      )}
    </section>
  )
}
