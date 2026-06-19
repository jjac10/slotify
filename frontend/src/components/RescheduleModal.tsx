import { useState, useEffect, useMemo } from 'react'
import { businessService } from '../services/businessService'
import { reservationService } from '../services/reservationService'
import { getApiError } from '../services/apiClient'
import type { ReservationResponse, AvailableSlot } from '../types/api'

interface Props {
  reservation: ReservationResponse
  onClose: () => void
  onRescheduled: (updated: ReservationResponse) => void
  contact?: string
}

function isoDate(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

function formatTime(iso: string): string {
  return new Date(iso).toLocaleTimeString('es-ES', { hour: '2-digit', minute: '2-digit' })
}

export function RescheduleModal({ reservation, onClose, onRescheduled, contact }: Props) {
  const today = useMemo(() => isoDate(new Date()), [])
  const [date, setDate] = useState(isoDate(new Date(reservation.startTime)))
  const [slots, setSlots] = useState<AvailableSlot[] | null>(null)
  const [loadingSlots, setLoadingSlots] = useState(false)
  const [slotsError, setSlotsError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const [saveError, setSaveError] = useState<string | null>(null)

  useEffect(() => {
    if (!date) return
    let active = true
    setSlots(null)
    setSlotsError(null)
    setLoadingSlots(true)
    businessService
      .availability(reservation.businessId, {
        serviceId: reservation.serviceId,
        staffId: reservation.staffId,
        date,
      })
      .then((data) => { if (active) setSlots(data) })
      .catch((err) => { if (active) setSlotsError(getApiError(err)?.message ?? 'No se pudo cargar la disponibilidad.') })
      .finally(() => { if (active) setLoadingSlots(false) })
    return () => { active = false }
  }, [date, reservation.businessId, reservation.serviceId, reservation.staffId])

  async function pickSlot(slotStart: string) {
    setSaveError(null)
    setSaving(true)
    try {
      const updated = await reservationService.reschedule(reservation.id, slotStart, contact)
      onRescheduled(updated)
    } catch (err) {
      setSaveError(getApiError(err)?.message ?? 'No se pudo reprogramar. Inténtalo de nuevo.')
      setSaving(false)
    }
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-end sm:items-center justify-center p-4 bg-black/40 backdrop-blur-sm"
      onClick={(e) => e.target === e.currentTarget && onClose()}
    >
      <div className="card w-full max-w-sm flex flex-col gap-stack-md">
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-bold">Reprogramar cita</h2>
          <button
            type="button"
            onClick={onClose}
            className="p-1 rounded-lg hover:bg-surface-container-low"
            aria-label="Cerrar"
          >
            <span className="material-symbols-outlined text-[22px] text-on-surface-variant">close</span>
          </button>
        </div>

        {(reservation.serviceName || reservation.staffName) && (
          <p className="text-sm text-on-surface-variant -mt-stack-sm">
            {reservation.serviceName && <span className="font-medium text-on-surface">{reservation.serviceName}</span>}
            {reservation.staffName && ` · ${reservation.staffName}`}
          </p>
        )}

        <div className="field">
          <label className="field-label" htmlFor="reschedule-date">Nueva fecha</label>
          <input
            id="reschedule-date"
            type="date"
            className="field-input"
            data-testid="reschedule-date-input"
            value={date}
            min={today}
            onChange={(e) => setDate(e.target.value)}
          />
        </div>

        {slotsError && <p role="alert" className="alert">{slotsError}</p>}
        {loadingSlots && <p className="text-sm text-on-surface-variant">Cargando huecos…</p>}

        {!loadingSlots && slots !== null && slots.length === 0 && (
          <p className="text-sm text-on-surface-variant">No hay huecos disponibles para este día.</p>
        )}

        {!loadingSlots && slots !== null && slots.length > 0 && (
          <div className="flex flex-wrap gap-2" data-testid="reschedule-slots">
            {slots.map((s) => (
              <button
                key={s.start}
                type="button"
                onClick={() => pickSlot(s.start)}
                disabled={saving}
                data-testid="reschedule-slot"
                className="rounded-lg px-3 py-2 text-sm font-semibold bg-primary-container/20 text-primary hover:bg-primary-container/40 disabled:opacity-50 transition-colors"
              >
                {formatTime(s.start)}
              </button>
            ))}
          </div>
        )}

        {saveError && <p role="alert" className="alert">{saveError}</p>}

        <button type="button" onClick={onClose} className="btn-ghost self-start text-sm -mt-stack-sm">
          Cerrar
        </button>
      </div>
    </div>
  )
}
