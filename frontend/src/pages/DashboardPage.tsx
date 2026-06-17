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
        <p className="text-on-surface-variant">Esta sección es solo para propietarios de un negocio.</p>
      </section>
    )
  }

  return (
    <section>
      <h1>Panel</h1>
      <p className="text-on-surface-variant mb-stack-md">Resumen de tu negocio.</p>

      {error && (
        <p role="alert" className="alert" data-testid="dashboard-error">
          {error}
        </p>
      )}

      {dashboard === null && !error && <p className="text-on-surface-variant">Cargando…</p>}

      {dashboard !== null && (
        <>
          {/* Stats — bento grid */}
          <div className="grid grid-cols-3 gap-stack-sm" data-testid="dashboard-metrics">
            <div className="card flex flex-col items-center text-center" data-testid="metric-total-reservations">
              <span className="text-xs text-on-surface-variant">Reservas</span>
              <span className="font-display text-2xl font-bold text-primary">{dashboard.totalReservations}</span>
            </div>
            <div className="card flex flex-col items-center text-center" data-testid="metric-reservations-month">
              <span className="text-xs text-on-surface-variant">Este mes</span>
              <span className="font-display text-2xl font-bold text-secondary">{dashboard.reservationsThisMonth}</span>
            </div>
            <div className="card flex flex-col items-center text-center" data-testid="metric-monthly-revenue">
              <span className="text-xs text-on-surface-variant">Ingresos</span>
              <span className="font-display text-xl font-bold text-primary">{formatEuro(dashboard.estimatedMonthlyRevenue)}</span>
            </div>
          </div>

          <h2 className="mt-stack-lg mb-stack-sm">Próximas reservas</h2>
          {dashboard.upcomingReservations.length === 0 ? (
            <div className="card flex flex-col items-center text-center py-stack-lg" data-testid="dashboard-upcoming-empty">
              <span className="material-symbols-outlined text-[36px] text-on-surface-variant/50">event_upcoming</span>
              <p className="mt-stack-sm text-sm text-on-surface-variant">No hay próximas reservas.</p>
            </div>
          ) : (
            <ul className="flex flex-col gap-stack-sm" data-testid="dashboard-upcoming-list">
              {dashboard.upcomingReservations.map((reservation) => (
                <li key={reservation.id} className="glass-card rounded-xl p-stack-md flex items-center gap-stack-md" data-testid="dashboard-upcoming-item">
                  <span className="w-11 h-11 rounded-full bg-primary-container/15 text-primary flex items-center justify-center shrink-0">
                    <span className="material-symbols-outlined">schedule</span>
                  </span>
                  <p className="flex-1 min-w-0 font-semibold">{formatDateTime(reservation.startTime)}</p>
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
