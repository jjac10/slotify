import { useState, useEffect, useCallback, useMemo } from 'react'
import type { FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { businessService } from '../services/businessService'
import { reservationService } from '../services/reservationService'
import { getApiError } from '../services/apiClient'
import type { ServiceResponse, AvailableSlot } from '../types/api'
import { useAuth } from '../hooks/useAuth'

type Step =
  | 'enter-business'
  | 'select-service'
  | 'select-staff'
  | 'select-datetime'
  | 'guest-info'
  | 'confirmed'

interface BookingState {
  businessId: string
  serviceId: string
  staffId: string
  slotStart: string
  guestName: string
  guestPhone: string
  guestEmail: string
}

type PartialBooking = Partial<BookingState> & { businessId?: string }

const STEP_LABELS: Partial<Record<Step, string>> = {
  'select-service': 'Servicio',
  'select-staff': 'Profesional',
  'select-datetime': 'Fecha y hora',
  'guest-info': 'Tus datos',
}

function isoDate(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
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
  const [selectedDate, setSelectedDate] = useState('')
  const [showPicker, setShowPicker] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const todayIso = useMemo(() => isoDate(new Date()), [])
  const [anchorDate, setAnchorDate] = useState(todayIso)

  // 7 días a partir del día ancla (hoy por defecto; se mueve al elegir en "Ver todo").
  const days = useMemo(() => {
    const base = new Date(`${anchorDate}T00:00:00`)
    return Array.from({ length: 7 }, (_, i) => {
      const d = new Date(base.getFullYear(), base.getMonth(), base.getDate() + i)
      return {
        iso: isoDate(d),
        dow: d.toLocaleDateString('es-ES', { weekday: 'short' }).replace('.', ''),
        day: d.getDate(),
      }
    })
  }, [anchorDate])

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

  const loadStaff = useCallback(async (businessId: string, serviceId?: string) => {
    setError(null)
    setLoading(true)
    try {
      setStaff(await businessService.listStaff(businessId, serviceId))
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
      setSlots(await businessService.availability(businessId, { serviceId, staffId, date }))
    } catch (err) {
      setError(getApiError(err)?.message ?? 'No se pudieron cargar los horarios disponibles.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    if (step === 'select-service' && booking.businessId) loadServices(booking.businessId)
  }, [step, booking.businessId, loadServices])

  function selectDay(iso: string) {
    setSelectedDate(iso)
    if (booking.businessId && booking.serviceId && booking.staffId) {
      loadSlots(booking.businessId, booking.serviceId, booking.staffId, iso)
    }
  }

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

  function formatDateTime(iso: string): string {
    return new Date(iso).toLocaleString('es-ES', { weekday: 'long', day: 'numeric', month: 'long', hour: '2-digit', minute: '2-digit' })
  }

  const monthLabel = selectedDate
    ? new Date(`${selectedDate}T00:00:00`).toLocaleDateString('es-ES', { month: 'long', year: 'numeric' })
    : ''
  const selectedService = services?.find((s) => s.id === booking.serviceId)
  const selectedStaff = staff?.find((s) => s.id === booking.staffId)

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

      {/* PASO 1 — Elegir negocio (desde Explorar, no por ID) */}
      {step === 'enter-business' && (
        <div className="card flex flex-col items-center text-center py-stack-xl">
          <span className="material-symbols-outlined text-[40px] text-on-surface-variant/40">storefront</span>
          <p className="mt-stack-sm font-semibold">Elige primero un negocio</p>
          <p className="text-sm text-on-surface-variant">Busca el negocio donde quieres reservar.</p>
          <Link to="/explorar" className="btn-primary mt-stack-md" data-testid="reserve-go-explore">
            Explorar negocios
          </Link>
        </div>
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
                      merge({ serviceId: svc.id, staffId: '' })
                      if (booking.businessId) loadStaff(booking.businessId, svc.id)
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
                      const first = days[0].iso
                      setSelectedDate(first)
                      if (booking.businessId && booking.serviceId) loadSlots(booking.businessId, booking.serviceId, s.id, first)
                      setStep('select-datetime')
                    }}>
                    Elegir
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}

      {/* PASO 4 — Fecha y hora (tira de días + slots debajo) */}
      {step === 'select-datetime' && (
        <div>
          <BackButton to="select-staff" />

          {/* Mes + ver todo (calendario completo) */}
          <div className="mb-stack-sm flex items-center justify-between">
            <h2 className="!mt-0 !text-base capitalize">{monthLabel}</h2>
            <button
              type="button"
              className="text-sm font-semibold text-primary hover:underline"
              data-testid="reserve-show-all"
              onClick={() => setShowPicker((v) => !v)}
            >
              {showPicker ? 'Ver menos' : 'Ver todo'}
            </button>
          </div>

          {showPicker && (
            <input
              type="date"
              className="field-input mb-stack-md"
              data-testid="reserve-date-input"
              min={todayIso}
              value={selectedDate}
              onChange={(e) => {
                if (e.target.value) {
                  setAnchorDate(e.target.value)
                  selectDay(e.target.value)
                  setShowPicker(false)
                }
              }}
            />
          )}

          {/* Tira de días (responsiva: 5 en móvil, 7 en escritorio, sin scroll) */}
          <div className="mb-stack-md flex gap-2" data-testid="reserve-days">
            {days.map((d, i) => {
              const active = selectedDate === d.iso
              return (
                <button
                  key={d.iso}
                  type="button"
                  data-testid="date-card"
                  data-date={d.iso}
                  onClick={() => selectDay(d.iso)}
                  className={`min-w-0 flex-1 flex-col items-center rounded-xl border px-1 py-2 transition-colors ${
                    i >= 5 ? 'hidden sm:flex' : 'flex'
                  } ${
                    active
                      ? 'border-transparent bg-primary-container text-on-primary'
                      : 'border-outline-variant bg-surface-container-lowest text-on-surface hover:border-primary-container'
                  }`}
                >
                  <span className="text-[11px] font-medium uppercase">{d.dow}</span>
                  <span className="text-lg font-bold leading-tight">{d.day}</span>
                </button>
              )
            })}
          </div>

          {/* Slots del día seleccionado */}
          <h2 className="!text-base mb-stack-sm">Horarios disponibles</h2>
          {slots && slots.length > 0 && (
            <ul className="grid grid-cols-3 gap-stack-sm sm:grid-cols-4" data-testid="reserve-slots">
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
          {slots !== null && slots.length === 0 && !loading && (
            <p className="text-on-surface-variant" data-testid="reserve-no-slots">
              No hay horarios disponibles ese día. Prueba con otro.
            </p>
          )}
        </div>
      )}

      {/* PASO 5 — Datos de invitado */}
      {step === 'guest-info' && (
        <form className="card flex flex-col gap-stack-md max-w-md" onSubmit={submitGuestBooking}>
          <BackButton to="select-datetime" />

          {/* Resumen de la reserva que se está haciendo */}
          <div className="rounded-xl bg-surface-container-low p-stack-md" data-testid="reserve-summary">
            <p className="flex items-center gap-2 font-bold">
              <span className="material-symbols-outlined text-[20px] text-primary">event_available</span>
              {selectedService?.name ?? 'Reserva'}
            </p>
            <p className="mt-1 text-sm text-on-surface-variant first-letter:uppercase">
              {selectedStaff ? `con ${selectedStaff.name}` : ''}
              {booking.slotStart ? ` · ${formatDateTime(booking.slotStart)}` : ''}
            </p>
          </div>

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

      {/* PASO 6 — Confirmado */}
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
