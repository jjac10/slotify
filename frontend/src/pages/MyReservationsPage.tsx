import { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { reservationService } from '../services/reservationService'
import { getApiError } from '../services/apiClient'
import { useAuth } from '../hooks/useAuth'
import { StatusPill } from '../components/StatusPill'
import type { ReservationResponse } from '../types/api'

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('es-ES', { day: 'numeric', month: 'long', year: 'numeric' })
}
function formatTime(iso: string): string {
  return new Date(iso).toLocaleTimeString('es-ES', { hour: '2-digit', minute: '2-digit' })
}

function ReservationCard({ r }: { r: ReservationResponse }) {
  return (
    <li className="card flex items-center gap-stack-md" data-testid="reservation-item">
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
  )
}

export function MyReservationsPage() {
  const { status } = useAuth()
  if (status === 'loading') return <p className="text-on-surface-variant">Cargando…</p>
  return status === 'authenticated' ? <AuthedReservations /> : <GuestLookup />
}

type Tab = 'upcoming' | 'past'

function AuthedReservations() {
  const [reservations, setReservations] = useState<ReservationResponse[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [tab, setTab] = useState<Tab>('upcoming')

  useEffect(() => {
    let active = true
    reservationService
      .listMine()
      .then((data) => active && setReservations(data))
      .catch((err) => active && setError(getApiError(err)?.message ?? 'No se pudieron cargar tus reservas.'))
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
        </div>
      )}
      {visible !== null && visible.length > 0 && (
        <ul className="flex flex-col gap-stack-sm" data-testid="my-reservations-list">
          {visible.map((r) => (
            <ReservationCard key={r.id} r={r} />
          ))}
        </ul>
      )}
    </section>
  )
}

function GuestLookup() {
  const [contact, setContact] = useState('')
  const [results, setResults] = useState<ReservationResponse[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (!contact.trim()) return
    setError(null)
    setLoading(true)
    try {
      setResults(await reservationService.lookupGuest(contact.trim()))
    } catch (err) {
      setError(getApiError(err)?.message ?? 'No se pudo buscar. Inténtalo de nuevo.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <section>
      <h1>Mis reservas</h1>
      <p className="text-on-surface-variant mb-stack-md">
        ¿Reservaste sin cuenta? Busca tus citas con tu teléfono o email.
      </p>

      <form onSubmit={handleSubmit} className="card flex flex-col gap-stack-md max-w-md" data-testid="guest-lookup-form">
        <div className="field">
          <label className="field-label" htmlFor="guest-lookup-contact">Teléfono o email</label>
          <input
            id="guest-lookup-contact"
            type="text"
            className="field-input"
            data-testid="guest-lookup-contact"
            value={contact}
            onChange={(e) => setContact(e.target.value)}
            placeholder="+34 600 000 000 o tu@email.com"
            required
          />
        </div>
        <button type="submit" className="btn-primary self-start" data-testid="guest-lookup-submit" disabled={loading}>
          {loading ? 'Buscando…' : 'Buscar mis reservas'}
        </button>
      </form>

      {error && (
        <p role="alert" className="alert mt-stack-md" data-testid="guest-lookup-error">
          {error}
        </p>
      )}

      {results !== null && results.length === 0 && (
        <div className="card mt-stack-md flex flex-col items-center text-center py-stack-lg" data-testid="guest-lookup-empty">
          <span className="material-symbols-outlined text-[36px] text-on-surface-variant/40">search_off</span>
          <p className="mt-stack-sm text-sm text-on-surface-variant">No encontramos reservas con ese contacto.</p>
        </div>
      )}
      {results !== null && results.length > 0 && (
        <ul className="mt-stack-md flex flex-col gap-stack-sm" data-testid="guest-lookup-list">
          {results.map((r) => (
            <ReservationCard key={r.id} r={r} />
          ))}
        </ul>
      )}
    </section>
  )
}
