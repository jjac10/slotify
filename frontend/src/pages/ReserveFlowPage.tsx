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
      const list = await businessService.listServices(businessId)
      setServices(list)
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
      const list = await businessService.listStaff(businessId)
      setStaff(list)
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
      if (list.length === 0) {
        setError('No hay slots disponibles para esa fecha.')
      }
    } catch (err) {
      setError(getApiError(err)?.message ?? 'No se pudieron cargar los horarios disponibles.')
    } finally {
      setLoading(false)
    }
  }, [])

  // Carga servicios cuando se tiene businessId
  useEffect(() => {
    if (step === 'select-service' && booking.businessId) {
      loadServices(booking.businessId)
    }
  }, [step, booking.businessId, loadServices])

  // Al seleccionar un slot como usuario autenticado, crea la reserva directamente.
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

  // Para invitados, el form recoge los datos y luego crea.
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

  return (
    <section className="reserve-flow">
      <h1>Reservar</h1>

      {error && (
        <p role="alert" data-testid="reserve-error">
          {error}
        </p>
      )}
      {loading && <p aria-live="polite">Cargando…</p>}

      {/* PASO 1 — Ingresar ID del negocio */}
      {step === 'enter-business' && (
        <form
          onSubmit={(e) => {
            e.preventDefault()
            if (booking.businessId) setStep('select-service')
          }}
        >
          <label>
            ID del negocio
            <input
              type="text"
              data-testid="reserve-business-id"
              value={booking.businessId ?? ''}
              onChange={(e) => merge({ businessId: e.target.value })}
              placeholder="uuid del negocio"
              required
            />
          </label>
          <button type="submit" data-testid="reserve-confirm-business">
            Continuar
          </button>
        </form>
      )}

      {/* PASO 2 — Elegir servicio */}
      {step === 'select-service' && (
        <div>
          <button type="button" onClick={() => setStep('enter-business')}>← Volver</button>
          <h2>Elige un servicio</h2>
          {services && services.length > 0 && (
            <ul data-testid="reserve-services">
              {services.map((svc) => (
                <li
                  key={svc.id}
                  data-testid="service-item"
                  data-service-id={svc.id}
                >
                  <span><strong>{svc.name}</strong> · {svc.durationMinutes} min{svc.price !== null ? ` · ${svc.price} €` : ''}</span>
                  <button
                    type="button"
                    data-testid="select-service"
                    onClick={() => {
                      merge({ serviceId: svc.id })
                      if (booking.businessId) loadStaff(booking.businessId)
                      setStep('select-staff')
                    }}
                  >
                    Elegir
                  </button>
                </li>
              ))}
            </ul>
          )}
          {services !== null && services.length === 0 && (
            <p data-testid="reserve-no-services">Este negocio no tiene servicios disponibles.</p>
          )}
        </div>
      )}

      {/* PASO 3 — Elegir trabajador */}
      {step === 'select-staff' && (
        <div>
          <button type="button" onClick={() => setStep('select-service')}>← Volver</button>
          <h2>Elige un trabajador</h2>
          {staff && staff.length > 0 && (
            <ul data-testid="reserve-staff-list">
              {staff.map((s) => (
                <li
                  key={s.id}
                  data-testid="staff-item"
                  data-staff-id={s.id}
                >
                  <span><strong>{s.name}</strong> ({s.role})</span>
                  <button
                    type="button"
                    data-testid="select-staff"
                    onClick={() => {
                      merge({ staffId: s.id })
                      setStep('select-date')
                    }}
                  >
                    Elegir
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}

      {/* PASO 4 — Elegir fecha */}
      {step === 'select-date' && (
        <div>
          <button type="button" onClick={() => setStep('select-staff')}>← Volver</button>
          <h2>Elige una fecha</h2>
          <div data-testid="reserve-date-picker">
            <label>
              Fecha
              <input
                type="date"
                data-testid="reserve-date-input"
                value={booking.date ?? ''}
                onChange={(e) => merge({ date: e.target.value })}
                required
              />
            </label>
            <button
              type="button"
              data-testid="reserve-load-slots"
              disabled={!booking.date || loading}
              onClick={() => {
                if (booking.businessId && booking.serviceId && booking.staffId && booking.date) {
                  loadSlots(booking.businessId, booking.serviceId, booking.staffId, booking.date)
                  setStep('select-slot')
                }
              }}
            >
              Ver horarios disponibles
            </button>
          </div>
        </div>
      )}

      {/* PASO 5 — Elegir slot */}
      {step === 'select-slot' && (
        <div>
          <button type="button" onClick={() => setStep('select-date')}>← Volver</button>
          <h2>Elige un horario</h2>
          {slots && slots.length > 0 && (
            <ul data-testid="reserve-slots">
              {slots.map((slot) => (
                <li
                  key={slot.start}
                  data-testid="slot-item"
                  data-start={slot.start}
                >
                  <span>
                    {new Date(slot.start).toLocaleTimeString('es-ES', { hour: '2-digit', minute: '2-digit' })}
                    {' — '}
                    {new Date(slot.end).toLocaleTimeString('es-ES', { hour: '2-digit', minute: '2-digit' })}
                  </span>
                  <button
                    type="button"
                    data-testid="select-slot"
                    disabled={loading}
                    onClick={() => {
                      merge({ slotStart: slot.start })
                      if (status === 'authenticated') {
                        bookSlot(slot.start)
                      } else {
                        setStep('guest-info')
                      }
                    }}
                  >
                    Reservar
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}

      {/* PASO 6 — Datos de invitado */}
      {step === 'guest-info' && (
        <form onSubmit={submitGuestBooking}>
          <button type="button" onClick={() => setStep('select-slot')}>← Volver</button>
          <h2>Tus datos</h2>
          <label>
            Nombre
            <input
              type="text"
              data-testid="reserve-guest-name"
              value={booking.guestName ?? ''}
              onChange={(e) => merge({ guestName: e.target.value })}
              required
            />
          </label>
          <label>
            Teléfono (o email)
            <input
              type="tel"
              data-testid="reserve-guest-phone"
              value={booking.guestPhone ?? ''}
              onChange={(e) => merge({ guestPhone: e.target.value })}
            />
          </label>
          <label>
            Email
            <input
              type="email"
              data-testid="reserve-guest-email"
              value={booking.guestEmail ?? ''}
              onChange={(e) => merge({ guestEmail: e.target.value })}
            />
          </label>
          <button type="submit" data-testid="reserve-confirm-booking" disabled={loading}>
            {loading ? 'Creando reserva…' : 'Confirmar reserva'}
          </button>
        </form>
      )}

      {/* PASO 7 — Reserva creada */}
      {step === 'confirmed' && (
        <div data-testid="booking-confirmed">
          <h2>✓ Reserva confirmada</h2>
          <p>Tu reserva se ha creado correctamente.</p>
          <a href="/mis-reservas">Ver mis reservas</a>
        </div>
      )}
    </section>
  )
}
