import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { reservationService } from '../services/reservationService'
import { getApiError } from '../services/apiClient'
import { useAuth } from '../hooks/useAuth'
import { StatusPill } from '../components/StatusPill'
import { RescheduleModal } from '../components/RescheduleModal'
import { ReviewModal } from '../components/ReviewModal'
import { reviewService } from '../services/reviewService'
import { GuestContactInput, buildGuestContact, isContactValid, type ContactMode } from '../components/GuestContactInput'
import type { MyReviewResponse, ReservationResponse, ReservationScope } from '../types/api'

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
  /** Si se pasa, la cita es pasada y se puede valorar (o editar la valoración del negocio). */
  onReview?: () => void
  /** Reseña existente del cliente para este negocio (si ya valoró). */
  existingReview?: MyReviewResponse
  contact?: string
}

function ReservationCard({ r, onCancelled, onReschedule, onReview, existingReview, contact }: CardProps) {
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
        <div className="flex items-center gap-2 pt-1 border-t border-outline-variant/30">
          {existingReview ? (
            <>
              <span className="flex items-center gap-1 px-3 py-1.5 text-xs font-semibold text-secondary" data-testid="reviewed-badge">
                <span className="material-symbols-outlined text-[16px] fill">check_circle</span>
                Ya valoraste este negocio
              </span>
              <button
                type="button"
                onClick={onReview}
                className="flex items-center gap-1 rounded-lg px-3 py-1.5 text-xs font-semibold text-primary hover:bg-primary-container/15 transition-colors"
                data-testid="review-edit-btn"
              >
                <span className="material-symbols-outlined text-[16px]">edit</span>
                Editar valoración
              </button>
            </>
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

/** Tamaño de página del listado (el backend clampa a un máximo de 50). */
const PAGE_SIZE = 20

const SCOPE_EMPTY: Record<ReservationScope, { icon: string; text: string }> = {
  upcoming: { icon: 'event_busy', text: 'No tienes reservas próximas.' },
  past: { icon: 'history', text: 'No tienes reservas pasadas.' },
  all: { icon: 'event', text: 'Todavía no tienes reservas.' },
}

function AuthedReservations() {
  const [reservations, setReservations] = useState<ReservationResponse[] | null>(null)
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [loadingMore, setLoadingMore] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [scope, setScope] = useState<ReservationScope>('upcoming')
  const [rescheduleTarget, setRescheduleTarget] = useState<ReservationResponse | null>(null)
  const [reviewTarget, setReviewTarget] = useState<ReservationResponse | null>(null)
  // Reseña del cliente por negocio (una por negocio): para mostrar "ya valoraste"/editar.
  const [reviewsByBusiness, setReviewsByBusiness] = useState<Map<string, MyReviewResponse>>(new Map())

  // Pestaña vigente: descarta respuestas de "Cargar más" que lleguen después de
  // haber cambiado de pestaña (el cambio resetea a página 1).
  const scopeRef = useRef(scope)
  scopeRef.current = scope

  useEffect(() => {
    let active = true
    setReservations(null)
    setError(null)
    reservationService
      .listMine({ scope, page: 1, pageSize: PAGE_SIZE })
      .then((data) => {
        if (active) { setReservations(data.items); setTotal(data.total); setPage(1) }
      })
      .catch((err) => active && setError(getApiError(err)?.message ?? 'No se pudieron cargar tus reservas.'))
    return () => { active = false }
  }, [scope])

  function loadMore() {
    const current = scope
    setLoadingMore(true)
    reservationService
      .listMine({ scope: current, page: page + 1, pageSize: PAGE_SIZE })
      .then((data) => {
        if (scopeRef.current !== current) return // la pestaña cambió mientras cargaba
        setReservations((prev) => [...(prev ?? []), ...data.items])
        setTotal(data.total)
        setPage(data.page)
      })
      .catch((err) => setError(getApiError(err)?.message ?? 'No se pudieron cargar más reservas.'))
      .finally(() => setLoadingMore(false))
  }

  function loadMyReviews() {
    reviewService.listMine()
      .then((list) => setReviewsByBusiness(new Map(list.map((rv) => [rv.businessId, rv]))))
      .catch(() => { /* las reseñas son secundarias en esta pantalla */ })
  }

  useEffect(loadMyReviews, [])

  function handleCancelled(id: string) {
    setReservations((prev) => prev?.filter((r) => r.id !== id) ?? null)
    setTotal((t) => Math.max(0, t - 1))
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

      {/* Toggle segmentado Próximas | Pasadas | Todas → ?scope= del backend */}
      <div className="mb-stack-md inline-flex gap-1 rounded-full bg-surface-container p-1" data-testid="reservations-scope">
        {([['upcoming', 'Próximas'], ['past', 'Pasadas'], ['all', 'Todas']] as const).map(([value, label]) => (
          <button
            key={value}
            type="button"
            onClick={() => setScope(value)}
            data-testid={`scope-${value}`}
            className={`rounded-full px-4 py-1.5 text-sm font-bold transition-all ${
              scope === value ? 'bg-primary text-on-primary shadow-sm' : 'text-on-surface-variant hover:text-on-surface'
            }`}
          >
            {label}
          </button>
        ))}
      </div>

      {error && (
        <p role="alert" className="alert" data-testid="my-reservations-error">
          {error}
        </p>
      )}
      {reservations === null && !error && <p className="text-on-surface-variant">Cargando…</p>}
      {reservations !== null && reservations.length === 0 && (
        <div className="card flex flex-col items-center text-center py-stack-xl" data-testid="my-reservations-empty">
          <span className="material-symbols-outlined text-[40px] text-on-surface-variant/40">
            {SCOPE_EMPTY[scope].icon}
          </span>
          <p className="mt-stack-sm font-semibold">{SCOPE_EMPTY[scope].text}</p>
        </div>
      )}
      {reservations !== null && reservations.length > 0 && (
        <ul className="flex flex-col gap-stack-sm" data-testid="my-reservations-list">
          {reservations.map((r) => {
            // En "Todas" conviven citas pasadas y futuras: las acciones dependen
            // de cada reserva (futura → cancelar/reprogramar; pasada → valorar).
            const isPast = new Date(r.startTime).getTime() < Date.now()
            return (
              <ReservationCard
                key={r.id}
                r={r}
                onCancelled={!isPast ? handleCancelled : undefined}
                onReschedule={!isPast ? () => setRescheduleTarget(r) : undefined}
                onReview={isPast ? () => setReviewTarget(r) : undefined}
                existingReview={reviewsByBusiness.get(r.businessId)}
              />
            )
          })}
        </ul>
      )}

      {/* Cargar más: mientras haya reservas sin traer (items.length < total) */}
      {reservations !== null && reservations.length < total && (
        <div className="mt-stack-md flex justify-center">
          <button
            type="button"
            onClick={loadMore}
            disabled={loadingMore}
            data-testid="load-more-reservations"
            className="inline-flex items-center gap-1 rounded-full border border-outline-variant px-4 py-2 text-sm font-semibold text-on-surface-variant hover:bg-surface-container-low disabled:opacity-60"
          >
            <span className="material-symbols-outlined text-[18px]">expand_more</span>
            {loadingMore ? 'Cargando…' : `Cargar más (${reservations.length} de ${total})`}
          </button>
        </div>
      )}

      {rescheduleTarget && (
        <RescheduleModal
          reservation={rescheduleTarget}
          onClose={() => setRescheduleTarget(null)}
          onRescheduled={handleRescheduled}
        />
      )}

      {reviewTarget && (() => {
        const existing = reviewsByBusiness.get(reviewTarget.businessId)
        return (
          <ReviewModal
            businessName={reviewTarget.businessName ?? 'el negocio'}
            serviceName={reviewTarget.serviceName}
            reservationId={reviewTarget.id}
            reviewId={existing?.id}
            initialRating={existing?.rating}
            initialComment={existing?.comment}
            onClose={() => setReviewTarget(null)}
            onSaved={() => {
              loadMyReviews()
              setReviewTarget(null)
            }}
          />
        )
      })()}
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
