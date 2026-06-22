import { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { reservationService } from '../services/reservationService'
import { getApiError } from '../services/apiClient'
import { useAuth } from '../hooks/useAuth'
import { StatusPill } from '../components/StatusPill'
import { RescheduleModal } from '../components/RescheduleModal'
import { ReviewModal } from '../components/ReviewModal'
import { GuestContactInput, buildGuestContact, isContactValid, type ContactMode } from '../components/GuestContactInput'
import type { ReservationResponse } from '../types/api'

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('es-ES', { day: 'numeric', month: 'long', year: 'numeric' })
}
function formatTime(iso: string): string {
  return new Date(iso).toLocaleTimeString('es-ES', { hour: '2-digit', minute: '2-digit' })
}

interface CardProps {
  r: ReservationResponse
  onCancelled?: (id: string) => void
  onReschedule?: () => void
  /** Si se pasa, la cita es pasada y se puede valorar. */
  onReview?: () => void
  reviewed?: boolean
  contact?: string
}

function ReservationCard({ r, onCancelled, onReschedule, onReview, reviewed, contact }: CardProps) {
  const isActive = r.status === 'pending' || r.status === 'confirmed'
  const canAct = isActive && new Date(r.startTime).getTime() > Date.now()
  const [confirming, setConfirming] = useState(false)
  const [cancelling, setCancelling] = useState(false)
  const [cancelError, setCancelError] = useState<string | null>(null)

  async function handleCancel() {
    setCancelling(true)
    try {
      await reservationService.cancel(r.id, undefined, contact)
      onCancelled?.(r.id)
    } catch (err) {
      const apiErr = getApiError(err)
      setCancelError(
        apiErr?.error === 'window_closed'
          ? 'No puedes cancelar con tan poca antelación — la ventana mínima ya está cerrada.'
          : apiErr?.message ?? 'No se pudo cancelar.',
      )
      setCancelling(false)
    }
  }

  return (
    <li className="card flex flex-col gap-stack-sm" data-testid="reservation-item">
      <div className="flex items-center gap-stack-md">
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
      </div>

      {canAct && (onCancelled || onReschedule) && !confirming && (
        <div className="flex gap-1 pt-1 border-t border-outline-variant/30">
          {onReschedule && (
            <button
              type="button"
              onClick={onReschedule}
              className="flex items-center gap-1 rounded-lg px-3 py-1.5 text-xs font-semibold text-primary hover:bg-primary-container/15 transition-colors"
              data-testid="reschedule-btn"
            >
              <span className="material-symbols-outlined text-[16px]">edit_calendar</span>
              Reprogramar
            </button>
          )}
          {onCancelled && (
            <button
              type="button"
              onClick={() => setConfirming(true)}
              className="flex items-center gap-1 rounded-lg px-3 py-1.5 text-xs font-semibold text-error hover:bg-error-container/30 transition-colors"
              data-testid="cancel-btn"
            >
              <span className="material-symbols-outlined text-[16px]">cancel</span>
              Cancelar
            </button>
          )}
        </div>
      )}

      {onReview && !confirming && (
        <div className="flex items-center gap-1 pt-1 border-t border-outline-variant/30">
          {reviewed ? (
            <span className="flex items-center gap-1 px-3 py-1.5 text-xs font-semibold text-secondary" data-testid="reviewed-badge">
              <span className="material-symbols-outlined text-[16px] fill">check_circle</span>
              ¡Gracias por tu valoración!
            </span>
          ) : (
            <button
              type="button"
              onClick={onReview}
              className="flex items-center gap-1 rounded-lg px-3 py-1.5 text-xs font-semibold text-amber-600 hover:bg-amber-500/10 transition-colors"
              data-testid="review-btn"
            >
              <span className="material-symbols-outlined text-[16px]">star</span>
              Valorar
            </button>
          )}
        </div>
      )}

      {confirming && (
        <div className="flex flex-col gap-stack-sm pt-1 border-t border-outline-variant/30">
          <p className="text-sm font-medium">¿Cancelar esta reserva?</p>
          {cancelError && <p role="alert" className="alert text-xs">{cancelError}</p>}
          <div className="flex gap-2">
            <button
              type="button"
              onClick={handleCancel}
              disabled={cancelling}
              className="rounded-lg px-3 py-1.5 text-xs font-semibold bg-error text-on-error hover:brightness-105 disabled:opacity-50 transition-colors"
              data-testid="cancel-confirm-btn"
            >
              {cancelling ? 'Cancelando…' : 'Sí, cancelar'}
            </button>
            <button
              type="button"
              onClick={() => { setConfirming(false); setCancelError(null) }}
              disabled={cancelling}
              className="rounded-lg px-3 py-1.5 text-xs font-semibold text-on-surface-variant hover:bg-surface-container-low disabled:opacity-50 transition-colors"
            >
              Volver
            </button>
          </div>
        </div>
      )}
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
  const [rescheduleTarget, setRescheduleTarget] = useState<ReservationResponse | null>(null)
  const [reviewTarget, setReviewTarget] = useState<ReservationResponse | null>(null)
  const [reviewedIds, setReviewedIds] = useState<Set<string>>(new Set())

  useEffect(() => {
    let active = true
    reservationService
      .listMine()
      .then((data) => active && setReservations(data))
      .catch((err) => active && setError(getApiError(err)?.message ?? 'No se pudieron cargar tus reservas.'))
    return () => { active = false }
  }, [])

  const visible = useMemo(() => {
    if (!reservations) return null
    const now = Date.now()
    return reservations
      .filter((r) =>
        tab === 'upcoming' ? new Date(r.startTime).getTime() >= now : new Date(r.startTime).getTime() < now,
      )
      // Próximas: la cita más cercana primero; pasadas: la más reciente primero.
      .sort((a, b) => {
        const diff = new Date(a.startTime).getTime() - new Date(b.startTime).getTime()
        return tab === 'upcoming' ? diff : -diff
      })
  }, [reservations, tab])

  function handleCancelled(id: string) {
    setReservations((prev) => prev?.filter((r) => r.id !== id) ?? null)
  }

  function handleRescheduled(updated: ReservationResponse) {
    setReservations((prev) =>
      prev?.map((r) =>
        r.id === updated.id
          ? { ...r, startTime: updated.startTime, endTime: updated.endTime, status: updated.status }
          : r,
      ) ?? null,
    )
    setRescheduleTarget(null)
  }

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
            <ReservationCard
              key={r.id}
              r={r}
              onCancelled={tab === 'upcoming' ? handleCancelled : undefined}
              onReschedule={tab === 'upcoming' ? () => setRescheduleTarget(r) : undefined}
              onReview={tab === 'past' ? () => setReviewTarget(r) : undefined}
              reviewed={reviewedIds.has(r.id)}
            />
          ))}
        </ul>
      )}

      {rescheduleTarget && (
        <RescheduleModal
          reservation={rescheduleTarget}
          onClose={() => setRescheduleTarget(null)}
          onRescheduled={handleRescheduled}
        />
      )}

      {reviewTarget && (
        <ReviewModal
          reservation={reviewTarget}
          onClose={() => setReviewTarget(null)}
          onReviewed={(id) => {
            setReviewedIds((prev) => new Set(prev).add(id))
            setReviewTarget(null)
          }}
        />
      )}
    </section>
  )
}

function GuestLookup() {
  const [contactMode, setContactMode] = useState<ContactMode>('phone')
  const [phoneLocal, setPhoneLocal] = useState('')
  const [email, setEmail] = useState('')
  const [searchedContact, setSearchedContact] = useState('')
  const [results, setResults] = useState<ReservationResponse[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [rescheduleTarget, setRescheduleTarget] = useState<ReservationResponse | null>(null)

  const contactValid = isContactValid(contactMode, phoneLocal, email)

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (!contactValid) { setError('Introduce un teléfono (9 dígitos) o un email válido.'); return }
    setError(null)
    setLoading(true)
    const built = buildGuestContact(contactMode, phoneLocal, email)
    const normalized = built.guestPhone ?? built.guestEmail ?? ''
    try {
      const found = await reservationService.lookupGuest(normalized)
      found.sort((a, b) => new Date(a.startTime).getTime() - new Date(b.startTime).getTime())
      setResults(found)
      setSearchedContact(normalized)
    } catch (err) {
      setError(getApiError(err)?.message ?? 'No se pudo buscar. Inténtalo de nuevo.')
    } finally {
      setLoading(false)
    }
  }

  function handleCancelled(id: string) {
    setResults((prev) => prev?.filter((r) => r.id !== id) ?? null)
  }

  function handleRescheduled(updated: ReservationResponse) {
    setResults((prev) =>
      prev?.map((r) =>
        r.id === updated.id
          ? { ...r, startTime: updated.startTime, endTime: updated.endTime }
          : r,
      ) ?? null,
    )
    setRescheduleTarget(null)
  }

  return (
    <section>
      <h1>Mis reservas</h1>
      <p className="text-on-surface-variant mb-stack-md">
        ¿Reservaste sin cuenta? Busca tus citas con tu teléfono o email.
      </p>

      <form onSubmit={handleSubmit} className="card flex flex-col gap-stack-md max-w-md" data-testid="guest-lookup-form">
        <GuestContactInput
          mode={contactMode}
          onModeChange={setContactMode}
          phoneLocal={phoneLocal}
          onPhoneChange={setPhoneLocal}
          email={email}
          onEmailChange={setEmail}
          testidPrefix="guest-lookup"
        />
        <button type="submit" className="btn-primary self-start" data-testid="guest-lookup-submit" disabled={loading || !contactValid}>
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
            <ReservationCard
              key={r.id}
              r={r}
              contact={searchedContact}
              onCancelled={handleCancelled}
              onReschedule={() => setRescheduleTarget(r)}
            />
          ))}
        </ul>
      )}

      {rescheduleTarget && (
        <RescheduleModal
          reservation={rescheduleTarget}
          contact={searchedContact}
          onClose={() => setRescheduleTarget(null)}
          onRescheduled={handleRescheduled}
        />
      )}
    </section>
  )
}
