import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { businessService } from '../services/businessService'
import { reservationService } from '../services/reservationService'
import { getApiError } from '../services/apiClient'
import { useAuth } from '../hooks/useAuth'
import { StatusPill } from '../components/StatusPill'
import type { BusinessResponse, ReservationResponse } from '../types/api'

function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString('es-ES', { dateStyle: 'medium', timeStyle: 'short' })
}

export function MiSlotifyPage() {
  const { user } = useAuth()
  const [suggestions, setSuggestions] = useState<BusinessResponse[] | null>(null)
  const [reservations, setReservations] = useState<ReservationResponse[] | null>(null)

  useEffect(() => {
    let active = true
    businessService.searchPublic().then((b) => active && setSuggestions(b.slice(0, 4))).catch(() => active && setSuggestions([]))
    reservationService.listMine().then((r) => active && setReservations(r)).catch((err) => {
      getApiError(err) // swallow; mostramos lista vacía
      if (active) setReservations([])
    })
    return () => {
      active = false
    }
  }, [])

  const upcoming = useMemo(() => {
    if (!reservations) return null
    const now = Date.now()
    return reservations.filter((r) => new Date(r.startTime).getTime() >= now).slice(0, 3)
  }, [reservations])

  return (
    <section className="flex flex-col gap-stack-lg">
      <div>
        <h1>Mi Slotify</h1>
        <p className="text-on-surface-variant">Hola{user?.email ? `, ${user.email.split('@')[0]}` : ''} 👋</p>
      </div>

      {/* Buscar negocio */}
      <Link
        to="/explorar"
        className="card flex items-center gap-stack-md transition-shadow hover:shadow-lift"
      >
        <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-primary-container text-on-primary">
          <span className="material-symbols-outlined">search</span>
        </span>
        <div className="flex-1">
          <p className="font-bold">Buscar negocio</p>
          <p className="text-sm text-on-surface-variant">Encuentra dónde reservar</p>
        </div>
        <span className="material-symbols-outlined text-on-surface-variant">chevron_right</span>
      </Link>

      {/* Próximas citas */}
      <div>
        <div className="mb-stack-sm flex items-center justify-between">
          <h2 className="!mt-0">Próximas citas</h2>
          <Link to="/mis-reservas" className="text-sm font-semibold text-primary hover:underline">
            Ver todas
          </Link>
        </div>
        {upcoming === null && <p className="text-on-surface-variant">Cargando…</p>}
        {upcoming !== null && upcoming.length === 0 && (
          <div className="card flex flex-col items-center text-center py-stack-lg">
            <span className="material-symbols-outlined text-[36px] text-on-surface-variant/40">event_available</span>
            <p className="mt-stack-sm text-sm text-on-surface-variant">No tienes citas próximas.</p>
            <Link to="/explorar" className="btn-primary mt-stack-sm text-sm">Reservar ahora</Link>
          </div>
        )}
        {upcoming !== null && upcoming.length > 0 && (
          <ul className="flex flex-col gap-stack-sm">
            {upcoming.map((r) => (
              <li key={r.id} className="card flex items-center gap-stack-md">
                <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-full bg-primary-container/15 text-primary">
                  <span className="material-symbols-outlined">event</span>
                </span>
                <div className="min-w-0 flex-1">
                  <p className="truncate font-semibold">{r.businessName ?? 'Reserva'}</p>
                  <p className="truncate text-sm text-on-surface-variant">
                    {r.serviceName ? `${r.serviceName} · ` : ''}
                    {formatDateTime(r.startTime)}
                  </p>
                </div>
                <StatusPill status={r.status} />
              </li>
            ))}
          </ul>
        )}
      </div>

      {/* Sugerencias */}
      {suggestions !== null && suggestions.length > 0 && (
        <div>
          <h2 className="mb-stack-sm">Sugerencias</h2>
          <ul className="flex flex-col gap-stack-sm">
            {suggestions.map((b) => (
              <li key={b.id} className="card flex items-center gap-stack-md">
                <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-secondary-container/40 text-on-secondary-container">
                  <span className="material-symbols-outlined">storefront</span>
                </span>
                <p className="flex-1 min-w-0 truncate font-semibold">{b.name}</p>
                <Link to={`/reservar?businessId=${b.id}`} className="btn-secondary py-2 text-sm">
                  Reservar
                </Link>
              </li>
            ))}
          </ul>
        </div>
      )}
    </section>
  )
}
