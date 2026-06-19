import { useEffect, useState } from 'react'
import { useAuth } from '../hooks/useAuth'
import { reservationService } from '../services/reservationService'
import { getApiError } from '../services/apiClient'
import { StatusPill } from '../components/StatusPill'
import { RescheduleModal } from '../components/RescheduleModal'
import type { ReservationResponse } from '../types/api'

function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString('es-ES', { dateStyle: 'medium', timeStyle: 'short' })
}

interface ItemProps {
  reservation: ReservationResponse
  onCancelled: (id: string) => void
  onConfirmed: (updated: ReservationResponse) => void
  onReschedule: () => void
}

function AgendaItem({ reservation: r, onCancelled, onConfirmed, onReschedule }: ItemProps) {
  const isActive = r.status === 'pending' || r.status === 'confirmed'
  const canAct = isActive && new Date(r.startTime).getTime() > Date.now()
  const [confirmingCancel, setConfirmingCancel] = useState(false)
  const [cancelling, setCancelling] = useState(false)
  const [confirming, setConfirming] = useState(false)
  const [cancelError, setCancelError] = useState<string | null>(null)

  async function handleCancel() {
    setCancelling(true)
    try {
      await reservationService.cancel(r.id)
      onCancelled(r.id)
    } catch (err) {
      setCancelError(getApiError(err)?.message ?? 'No se pudo cancelar.')
      setCancelling(false)
    }
  }

  async function handleConfirm() {
    setConfirming(true)
    try {
      const updated = await reservationService.confirm(r.id)
      onConfirmed(updated)
    } catch (err) {
      setCancelError(getApiError(err)?.message ?? 'No se pudo confirmar.')
      setConfirming(false)
    }
  }

  return (
    <li className="glass-card rounded-xl p-stack-md flex flex-col gap-stack-sm" data-testid="agenda-item">
      <div className="flex items-center gap-stack-md">
        <span className="w-11 h-11 rounded-full bg-secondary-container/40 text-on-secondary-container flex items-center justify-center shrink-0">
          <span className="material-symbols-outlined">person</span>
        </span>
        <div className="flex-1 min-w-0">
          <p className="truncate font-semibold">
            {r.serviceName ?? 'Reserva'}
            {r.staffName ? ` · ${r.staffName}` : ''}
          </p>
          <p className="truncate text-sm text-on-surface-variant">
            {formatDateTime(r.startTime)} · {r.guestId ? 'Invitado' : 'Cliente'}
          </p>
        </div>
        <StatusPill status={r.status} />
      </div>

      {canAct && !confirmingCancel && (
        <div className="flex gap-1 pt-1 border-t border-outline-variant/30">
          {r.status === 'pending' && (
            <button
              type="button"
              onClick={handleConfirm}
              disabled={confirming}
              className="flex items-center gap-1 rounded-lg px-3 py-1.5 text-xs font-semibold text-emerald-700 hover:bg-emerald-100 disabled:opacity-50 transition-colors"
              data-testid="confirm-btn"
            >
              <span className="material-symbols-outlined text-[16px]">check_circle</span>
              {confirming ? 'Confirmando…' : 'Confirmar'}
            </button>
          )}
          <button
            type="button"
            onClick={onReschedule}
            className="flex items-center gap-1 rounded-lg px-3 py-1.5 text-xs font-semibold text-primary hover:bg-primary-container/15 transition-colors"
            data-testid="reschedule-btn"
          >
            <span className="material-symbols-outlined text-[16px]">edit_calendar</span>
            Reprogramar
          </button>
          <button
            type="button"
            onClick={() => setConfirmingCancel(true)}
            className="flex items-center gap-1 rounded-lg px-3 py-1.5 text-xs font-semibold text-error hover:bg-error-container/30 transition-colors"
            data-testid="cancel-btn"
          >
            <span className="material-symbols-outlined text-[16px]">cancel</span>
            Cancelar
          </button>
        </div>
      )}

      {confirmingCancel && (
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
              onClick={() => { setConfirmingCancel(false); setCancelError(null) }}
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

export function OwnerAgendaPage() {
  const { businessId, isOwner } = useAuth()
  const [reservations, setReservations] = useState<ReservationResponse[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [rescheduleTarget, setRescheduleTarget] = useState<ReservationResponse | null>(null)

  useEffect(() => {
    if (!businessId) return
    let active = true
    reservationService
      .listForBusiness(businessId)
      .then((data) => { if (active) setReservations(data) })
      .catch((err) => { if (active) setError(getApiError(err)?.message ?? 'No se pudo cargar la agenda.') })
    return () => { active = false }
  }, [businessId])

  if (!isOwner || !businessId) {
    return (
      <section>
        <h1>Agenda</h1>
        <p className="text-on-surface-variant">Esta sección es solo para propietarios de un negocio.</p>
      </section>
    )
  }

  function handleCancelled(id: string) {
    setReservations((prev) => prev?.filter((r) => r.id !== id) ?? null)
  }

  function handleConfirmed(updated: ReservationResponse) {
    setReservations((prev) =>
      prev?.map((r) => (r.id === updated.id ? { ...r, status: updated.status } : r)) ?? null,
    )
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
      <h1>Agenda del negocio</h1>
      <p className="text-on-surface-variant mb-stack-md">Todas las reservas de tu negocio.</p>

      {error && (
        <p role="alert" className="alert" data-testid="agenda-error">
          {error}
        </p>
      )}

      {reservations === null && !error && <p className="text-on-surface-variant">Cargando…</p>}

      {reservations !== null && reservations.length === 0 && (
        <div className="card flex flex-col items-center text-center py-stack-xl" data-testid="agenda-empty">
          <span className="material-symbols-outlined text-[40px] text-on-surface-variant/50">calendar_today</span>
          <p className="mt-stack-sm font-semibold">No hay reservas en la agenda todavía.</p>
        </div>
      )}

      {reservations !== null && reservations.length > 0 && (
        <ul className="flex flex-col gap-stack-sm" data-testid="agenda-list">
          {reservations.map((r) => (
            <AgendaItem
              key={r.id}
              reservation={r}
              onCancelled={handleCancelled}
              onConfirmed={handleConfirmed}
              onReschedule={() => setRescheduleTarget(r)}
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
    </section>
  )
}
