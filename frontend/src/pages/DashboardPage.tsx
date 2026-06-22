import { useEffect, useState } from 'react'
import { useAuth } from '../hooks/useAuth'
import { businessService } from '../services/businessService'
import { getApiError } from '../services/apiClient'
import { StatusPill } from '../components/StatusPill'
import { RatingStars } from '../components/Stars'
import type { DashboardResponse } from '../types/api'

function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString('es-ES', { dateStyle: 'medium', timeStyle: 'short' })
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('es-ES', { day: 'numeric', month: 'short', year: 'numeric' })
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
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-stack-sm" data-testid="dashboard-metrics">
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
            <div className="card flex flex-col items-center justify-center text-center" data-testid="metric-rating">
              <span className="text-xs text-on-surface-variant">Valoración</span>
              {dashboard.averageRating != null ? (
                <span className="font-display text-2xl font-bold text-amber-500">
                  {dashboard.averageRating.toFixed(1)}
                  <span className="ml-0.5 text-sm font-normal text-on-surface-variant">/5</span>
                </span>
              ) : (
                <span className="mt-1 text-xs text-on-surface-variant">Sin reseñas</span>
              )}
              <span className="text-[11px] text-on-surface-variant">{dashboard.reviewCount} reseña{dashboard.reviewCount === 1 ? '' : 's'}</span>
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
                  <div className="min-w-0 flex-1">
                    <p className="truncate font-semibold">
                      {reservation.serviceName ?? 'Reserva'}
                      {reservation.staffName ? ` · ${reservation.staffName}` : ''}
                    </p>
                    <p className="truncate text-sm text-on-surface-variant">{formatDateTime(reservation.startTime)}</p>
                  </div>
                  <StatusPill status={reservation.status} />
                </li>
              ))}
            </ul>
          )}

          {dashboard.reviewCount > 0 && (
            <>
              <div className="mt-stack-lg mb-stack-sm flex items-center justify-between">
                <h2 className="!mt-0 !mb-0">Reseñas recientes</h2>
                <RatingStars value={dashboard.averageRating} count={dashboard.reviewCount} />
              </div>
              <ul className="flex flex-col gap-stack-sm" data-testid="dashboard-reviews-list">
                {dashboard.recentReviews.map((review) => (
                  <li key={review.id} className="glass-card rounded-xl p-stack-md flex flex-col gap-1" data-testid="dashboard-review-item">
                    <div className="flex items-center justify-between gap-stack-md">
                      <span className="font-semibold text-sm">{review.authorName ?? 'Cliente'}</span>
                      <RatingStars value={review.rating} />
                    </div>
                    {review.comment && <p className="text-sm text-on-surface-variant">{review.comment}</p>}
                    <span className="text-[11px] text-on-surface-variant">{formatDate(review.createdAt)}</span>
                  </li>
                ))}
              </ul>
            </>
          )}
        </>
      )}
    </section>
  )
}
