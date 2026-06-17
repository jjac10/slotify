import { useState, useEffect, useCallback } from 'react'
import type { FormEvent } from 'react'
import { useSearchParams } from 'react-router-dom'
import { businessService } from '../services/businessService'
import { reservationService } from '../services/reservationService'
import { getApiError } from '../services/apiClient'
import type { ServiceResponse, AvailableSlot } from '../types/api'
import { useAuth } from '../hooks/useAuth'

type Step =
  | 'enter-business'
  | 'select-service'
  | 'select-staff'
  | 'select-date'
  | 'select-slot'
  | 'guest-info'
  | 'confirmed'

interface BookingState {
  businessId: string
  serviceId: string
  staffId: string
  date: string
  slotStart: string
  guestName: string
  guestPhone: string
  guestEmail: string
}

type PartialBooking = Partial<BookingState> & { businessId?: string }

const STEP_LABELS: Partial<Record<Step, string>> = {
  'select-service': 'Servicio',
  'select-staff': 'Profesional',
  'select-date': 'Fecha',
  'select-slot': 'Hora',
  'guest-info': 'Tus datos',
}

export function ReserveFlowPage() {
  const { status } = useAuth()
  const [searchParams] = useSearchParams()
  const initialBusinessId = searchParams.get('businessId') ?? ''

  const [step, setStep] = useState<Step>(initialBusinessId ? 'select-service' : 'enter-business')
  const [booking, setBooking] = useState<PartialBooking>({ businessId: initialBusinessId })

  const [services, setServices] = useState<ServiceResponse[] | null>(null)
  const [staff, setStaff] = useState<Array<{ id: string; name: string; role: string }> | null>(null)
  const [slots, setSlots] = useState<AvailableSlot[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const loadServices = useCallback(async (businessId: string) => {
    setError(null)
    setLoading(true)
    try {
      setServices(await businessService.listServices(businessId))
    } catch (err) {
      setError(getApiError(err)?.message ?? 'No se pudieron cargar los servicios.')
    } finally {
      setLoading(false)
    }
  }, [])

  const loadStaff = useCallback(async (businessId: string) => {
    setError(null)
    setLoading(true)
    try {
      setStaff(await businessService.listStaff(businessId))
    } catch (err) {
      setError(getApiError(err)?.message ?? 'No se pudieron cargar los trabajadores.')
    } finally {
      setLoading(false)
    }
  }, [])

  const loadSlots = useCallback(async (businessId: string, serviceId: string, staffId: string, date: string) => {
    setError(null)
    setSlots(null)
    setLoading(true)
    try {
      const list = await businessService.availability(businessId, { serviceId, staffId, date })
      setSlots(list)
      if (list.length === 0) setError('No hay slots disponibles para esa fecha.')
    } catch (err) {
      setError(getApiError(err)?.message ?? 'No se pudieron cargar los horarios disponibles.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    if (step === 'select-service' && booking.businessId) loadServices(booking.businessId)
  }, [step, booking.businessId, loadServices])

  async function bookSlot(slotStart: string) {
    if (!booking.businessId || !booking.serviceId || !booking.staffId) return
    setError(null)
    setLoading(true)
    try {
      await reservationService.create({
        businessId: booking.businessId,
        serviceId: booking.serviceId,
        staffId: booking.staffId,
        startTime: slotStart,
      })
      setStep('confirmed')
    } catch (err) {
      setError(getApiError(err)?.message ?? 'No se pudo crear la reserva.')
    } finally {
      setLoading(false)
    }
  }

  async function submitGuestBooking(e: FormEvent) {
    e.preventDefault()
    if (!booking.businessId || !booking.serviceId || !booking.staffId || !booking.slotStart) return
    setError(null)
    setLoading(true)
    try {
      await reservationService.create({
        businessId: booking.businessId,
        serviceId: booking.serviceId,
        staffId: booking.staffId,
        startTime: booking.slotStart,
        guestName: booking.guestName,
        guestPhone: booking.guestPhone || undefined,
        guestEmail: booking.guestEmail || undefined,
      })
      setStep('confirmed')
    } catch (err) {
      setError(getApiError(err)?.message ?? 'No se pudo crear la reserva.')
    } finally {
      setLoading(false)
    }
  }

  function merge(partial: PartialBooking) {
    setBooking((prev) => ({ ...prev, ...partial }))
  }

  function formatTime(iso: string): string {
    return new Date(iso).toLocaleTimeString('es-ES', { hour: '2-digit', minute: '2-digit' })
  }

  function BackButton({ to }: { to: Step }) {
    return (
      <button type="button" className="btn-ghost -ml-2 mb-stack-sm text-sm" onClick={() => setStep(to)}>
        <span className="material-symbols-outlined text-[18px]">arrow_back</span> Volver
      </button>
    )
  }

  return (
    <section>
      <h1>Reservar</h1>
      {STEP_LABELS[step] && <p className="text-on-surface-variant mb-stack-md">{STEP_LABELS[step]}</p>}

      {error && (
        <p role="alert" className="alert mb-stack-md" data-testid="reserve-error">
          {error}
        </p>
      )}
      {loading && <p className="text-on-surface-variant mb-stack-sm" aria-live="polite">Cargando…</p>}

      {/* PASO 1 — ID del negocio */}
      {step === 'enter-business' && (
        <form
          className="card flex flex-col gap-stack-md max-w-md"
          onSubmit={(e) => {
            e.preventDefault()
            if (booking.businessId) setStep('select-service')
          }}
        >
          <div className="field">
            <label className="field-label" htmlFor="reserve-business-id">ID del negocio</label>
            <input id="reserve-business-id" type="text" className="field-input" data-testid="reserve-business-id"
              value={booking.businessId ?? ''} onChange={(e) => merge({ businessId: e.target.value })}
              placeholder="uuid del negocio" required />
          </div>
          <button type="submit" className="btn-primary self-start" data-testid="reserve-confirm-business">
            Continuar
          </button>
        </form>
      )}

      {/* PASO 2 — Servicio */}
      {step === 'select-service' && (
        <div>
          <BackButton to="enter-business" />
          {services && services.length > 0 && (
            <ul className="flex flex-col gap-stack-sm" data-testid="reserve-services">
              {services.map((svc) => (
                <li key={svc.id} className="glass-card rounded-xl p-stack-md flex items-center gap-stack-md" data-testid="service-item" data-service-id={svc.id}>
                  <div className="flex-1 min-w-0">
                    <strong className="font-semibold">{svc.name}</strong>
                    <p className="text-sm text-on-surface-variant">
                      {svc.durationMinutes} min{svc.price !== null ? ` · ${svc.price} €` : ''}
                    </p>
                  </div>
                  <button type="button" className="btn-primary py-2 text-sm" data-testid="select-service"
                    onClick={() => {
                      merge({ serviceId: svc.id })
                      if (booking.businessId) loadStaff(booking.businessId)
                      setStep('select-staff')
                    }}>
                    Elegir
                  </button>
                </li>
              ))}
            </ul>
          )}
          {services !== null && services.length === 0 && (
            <p className="text-on-surface-variant" data-testid="reserve-no-services">Este negocio no tiene servicios disponibles.</p>
          )}
        </div>
      )}

      {/* PASO 3 — Profesional */}
      {step === 'select-staff' && (
        <div>
          <BackButton to="select-service" />
          {staff && staff.length > 0 && (
            <ul className="flex flex-col gap-stack-sm" data-testid="reserve-staff-list">
              {staff.map((s) => (
                <li key={s.id} className="glass-card rounded-xl p-stack-md flex items-center gap-stack-md" data-testid="staff-item" data-staff-id={s.id}>
                  <span className="w-10 h-10 rounded-full bg-secondary-container/40 text-on-secondary-container flex items-center justify-center shrink-0 font-bold">
                    {s.name.charAt(0).toUpperCase()}
                  </span>
                  <div className="flex-1 min-w-0">
                    <strong className="font-semibold">{s.name}</strong>
                    <p className="text-sm text-on-surface-variant">{s.role}</p>
                  </div>
                  <button type="button" className="btn-primary py-2 text-sm" data-testid="select-staff"
                    onClick={() => {
                      merge({ staffId: s.id })
                      setStep('select-date')
                    }}>
                    Elegir
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}

      {/* PASO 4 — Fecha */}
      {step === 'select-date' && (
        <div>
          <BackButton to="select-staff" />
          <div className="card flex flex-col gap-stack-md max-w-md" data-testid="reserve-date-picker">
            <div className="field">
              <label className="field-label" htmlFor="reserve-date-input">Fecha</label>
              <input id="reserve-date-input" type="date" className="field-input" data-testid="reserve-date-input"
                value={booking.date ?? ''} onChange={(e) => merge({ date: e.target.value })} required />
            </div>
            <button type="button" className="btn-primary self-start" data-testid="reserve-load-slots"
              disabled={!booking.date || loading}
              onClick={() => {
                if (booking.businessId && booking.serviceId && booking.staffId && booking.date) {
                  loadSlots(booking.businessId, booking.serviceId, booking.staffId, booking.date)
                  setStep('select-slot')
                }
              }}>
              Ver horarios disponibles
            </button>
          </div>
        </div>
      )}

      {/* PASO 5 — Hora */}
      {step === 'select-slot' && (
        <div>
          <BackButton to="select-date" />
          {slots && slots.length > 0 && (
            <ul className="grid grid-cols-3 sm:grid-cols-4 gap-stack-sm" data-testid="reserve-slots">
              {slots.map((slot) => (
                <li key={slot.start} data-testid="slot-item" data-start={slot.start}>
                  <button type="button" className="btn-secondary w-full py-2.5 text-sm" data-testid="select-slot" disabled={loading}
                    onClick={() => {
                      merge({ slotStart: slot.start })
                      if (status === 'authenticated') bookSlot(slot.start)
                      else setStep('guest-info')
                    }}>
                    {formatTime(slot.start)}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}

      {/* PASO 6 — Datos de invitado */}
      {step === 'guest-info' && (
        <form className="card flex flex-col gap-stack-md max-w-md" onSubmit={submitGuestBooking}>
          <BackButton to="select-slot" />
          <div className="field">
            <label className="field-label" htmlFor="reserve-guest-name">Nombre</label>
            <input id="reserve-guest-name" type="text" className="field-input" data-testid="reserve-guest-name"
              value={booking.guestName ?? ''} onChange={(e) => merge({ guestName: e.target.value })} required />
          </div>
          <div className="field">
            <label className="field-label" htmlFor="reserve-guest-phone">Teléfono</label>
            <input id="reserve-guest-phone" type="tel" className="field-input" data-testid="reserve-guest-phone"
              value={booking.guestPhone ?? ''} onChange={(e) => merge({ guestPhone: e.target.value })} />
          </div>
          <div className="field">
            <label className="field-label" htmlFor="reserve-guest-email">Email</label>
            <input id="reserve-guest-email" type="email" className="field-input" data-testid="reserve-guest-email"
              value={booking.guestEmail ?? ''} onChange={(e) => merge({ guestEmail: e.target.value })} />
          </div>
          <button type="submit" className="btn-primary" data-testid="reserve-confirm-booking" disabled={loading}>
            {loading ? 'Creando reserva…' : 'Confirmar reserva'}
          </button>
        </form>
      )}

      {/* PASO 7 — Confirmado */}
      {step === 'confirmed' && (
        <div className="card flex flex-col items-center text-center py-stack-xl" data-testid="booking-confirmed">
          <span className="w-14 h-14 rounded-full bg-primary-container/15 text-primary flex items-center justify-center">
            <span className="material-symbols-outlined text-[32px] fill">check_circle</span>
          </span>
          <h2 className="!mt-stack-md">Reserva confirmada</h2>
          <p className="text-on-surface-variant">Tu reserva se ha creado correctamente.</p>
          <a href="/mis-reservas" className="btn-primary mt-stack-md">Ver mis reservas</a>
        </div>
      )}
    </section>
  )
}
