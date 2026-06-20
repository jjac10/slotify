import { useState, useEffect, useMemo } from 'react'
import { businessService } from '../services/businessService'
import { reservationService } from '../services/reservationService'
import { getApiError } from '../services/apiClient'
import type { AvailableSlot, ServiceResponse, StaffMember } from '../types/api'

interface Props {
  businessId: string
  onClose: () => void
  onCreated: () => void
}

function isoDate(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

function formatTime(iso: string): string {
  return new Date(iso).toLocaleTimeString('es-ES', { hour: '2-digit', minute: '2-digit' })
}

/**
 * Reserva manual creada por el owner/staff desde la agenda, para un cliente
 * (datos de invitado). El backend la registra como reserva de invitado aunque
 * la petición esté autenticada.
 */
export function NewReservationModal({ businessId, onClose, onCreated }: Props) {
  const today = useMemo(() => isoDate(new Date()), [])
  const [services, setServices] = useState<ServiceResponse[] | null>(null)
  const [staff, setStaff] = useState<StaffMember[] | null>(null)
  const [serviceId, setServiceId] = useState('')
  const [staffId, setStaffId] = useState('')
  const [date, setDate] = useState(today)
  const [slots, setSlots] = useState<AvailableSlot[] | null>(null)
  const [slotStart, setSlotStart] = useState('')
  const [loadingSlots, setLoadingSlots] = useState(false)
  const [guestName, setGuestName] = useState('')
  const [contactMode, setContactMode] = useState<'phone' | 'email'>('phone')
  const [phoneLocal, setPhoneLocal] = useState('') // dígitos sin prefijo; el país aporta el +34
  const [email, setEmail] = useState('')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    businessService.listServices(businessId)
      .then(setServices)
      .catch((err) => setError(getApiError(err)?.message ?? 'No se pudieron cargar los servicios.'))
  }, [businessId])

  // Al elegir servicio: cargar quién lo realiza y reiniciar trabajador/hueco.
  useEffect(() => {
    if (!serviceId) { setStaff(null); return }
    setStaffId(''); setSlotStart(''); setSlots(null)
    businessService.listStaff(businessId, serviceId)
      .then(setStaff)
      .catch((err) => setError(getApiError(err)?.message ?? 'No se pudieron cargar los trabajadores.'))
  }, [serviceId, businessId])

  // Con servicio + trabajador + fecha: cargar huecos.
  useEffect(() => {
    if (!serviceId || !staffId || !date) { setSlots(null); return }
    let active = true
    setSlots(null); setSlotStart(''); setLoadingSlots(true)
    businessService.availability(businessId, { serviceId, staffId, date })
      .then((d) => { if (active) setSlots(d) })
      .catch((err) => { if (active) setError(getApiError(err)?.message ?? 'No se pudo cargar la disponibilidad.') })
      .finally(() => { if (active) setLoadingSlots(false) })
    return () => { active = false }
  }, [serviceId, staffId, date, businessId])

  const contactFilled = contactMode === 'phone' ? phoneLocal.trim() : email.trim()
  const canSubmit = Boolean(serviceId && staffId && slotStart && guestName.trim() && contactFilled) && !saving

  async function handleSubmit() {
    if (!canSubmit) return
    setSaving(true)
    setError(null)
    try {
      await reservationService.create({
        businessId,
        serviceId,
        staffId,
        startTime: slotStart,
        guestName: guestName.trim(),
        ...(contactMode === 'phone'
          ? { guestPhone: `+34${phoneLocal.replace(/\D/g, '')}` }
          : { guestEmail: email.trim() }),
      })
      onCreated()
    } catch (err) {
      setError(getApiError(err)?.message ?? 'No se pudo crear la reserva.')
      setSaving(false)
    }
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-end sm:items-center justify-center p-4 bg-black/40 backdrop-blur-sm"
      onClick={(e) => e.target === e.currentTarget && onClose()}
    >
      <div className="card w-full max-w-sm flex flex-col gap-stack-md max-h-[90vh] overflow-y-auto" data-testid="new-reservation-modal">
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-bold">Nueva reserva</h2>
          <button type="button" onClick={onClose} className="p-1 rounded-lg hover:bg-surface-container-low" aria-label="Cerrar">
            <span className="material-symbols-outlined text-[22px] text-on-surface-variant">close</span>
          </button>
        </div>

        <div className="field">
          <label className="field-label" htmlFor="nr-service">Servicio</label>
          <select id="nr-service" className="field-input" data-testid="nr-service"
            value={serviceId} onChange={(e) => setServiceId(e.target.value)}>
            <option value="">Elige un servicio…</option>
            {services?.map((s) => (
              <option key={s.id} value={s.id}>{s.name} · {s.durationMinutes} min</option>
            ))}
          </select>
        </div>

        {serviceId && (
          <div className="field">
            <label className="field-label" htmlFor="nr-staff">Profesional</label>
            <select id="nr-staff" className="field-input" data-testid="nr-staff"
              value={staffId} onChange={(e) => setStaffId(e.target.value)}>
              <option value="">Elige un profesional…</option>
              {staff?.map((s) => (
                <option key={s.id} value={s.id}>{s.name}</option>
              ))}
            </select>
          </div>
        )}

        {serviceId && staffId && (
          <div className="field">
            <label className="field-label" htmlFor="nr-date">Fecha</label>
            <input id="nr-date" type="date" className="field-input" data-testid="nr-date"
              value={date} min={today} onChange={(e) => setDate(e.target.value)} />
          </div>
        )}

        {serviceId && staffId && (
          <>
            {loadingSlots && <p className="text-sm text-on-surface-variant">Cargando huecos…</p>}
            {!loadingSlots && slots !== null && slots.length === 0 && (
              <p className="text-sm text-on-surface-variant">No hay huecos ese día.</p>
            )}
            {!loadingSlots && slots !== null && slots.length > 0 && (
              <div className="grid grid-cols-4 gap-2" data-testid="nr-slots">
                {slots.map((s) => (
                  <button key={s.start} type="button" onClick={() => setSlotStart(s.start)} data-testid="nr-slot"
                    className={`rounded-lg py-2 text-sm font-semibold text-center transition-colors ${
                      slotStart === s.start ? 'bg-primary text-on-primary' : 'bg-primary-container/20 text-primary hover:bg-primary-container/40'
                    }`}>
                    {formatTime(s.start)}
                  </button>
                ))}
              </div>
            )}
          </>
        )}

        {slotStart && (
          <>
            <div className="field">
              <label className="field-label" htmlFor="nr-guest-name">Nombre del cliente</label>
              <input id="nr-guest-name" type="text" className="field-input" data-testid="nr-guest-name"
                value={guestName} onChange={(e) => setGuestName(e.target.value)} placeholder="Nombre y apellidos" />
            </div>
            <div className="field">
              <label className="field-label">Contacto</label>
              <div className="inline-flex rounded-full border border-outline-variant/50 bg-surface-container p-1 gap-1 mb-2 self-start">
                {(['phone', 'email'] as const).map((m) => (
                  <button key={m} type="button" onClick={() => setContactMode(m)}
                    className={`rounded-full px-3 py-1 text-xs font-bold transition-colors ${
                      contactMode === m ? 'bg-primary text-on-primary' : 'text-on-surface-variant'
                    }`}>
                    {m === 'phone' ? 'Teléfono' : 'Email'}
                  </button>
                ))}
              </div>
              {contactMode === 'phone' ? (
                <div className="flex gap-2">
                  <select className="field-input w-28 shrink-0" data-testid="nr-country" aria-label="País" disabled>
                    <option value="ES">🇪🇸 +34</option>
                  </select>
                  <input type="tel" inputMode="numeric" className="field-input flex-1" data-testid="nr-contact"
                    value={phoneLocal} onChange={(e) => setPhoneLocal(e.target.value.replace(/\D/g, '').slice(0, 9))}
                    placeholder="600 000 000" />
                </div>
              ) : (
                <input type="email" className="field-input" data-testid="nr-contact"
                  value={email} onChange={(e) => setEmail(e.target.value)} placeholder="cliente@email.com" />
              )}
            </div>
          </>
        )}

        {error && <p role="alert" className="alert">{error}</p>}

        <button type="button" onClick={handleSubmit} disabled={!canSubmit} className="btn-primary" data-testid="nr-submit">
          {saving ? 'Creando…' : 'Crear reserva'}
        </button>
      </div>
    </div>
  )
}
