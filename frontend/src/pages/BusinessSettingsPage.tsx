import { useCallback, useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { useAuth } from '../hooks/useAuth'
import { businessService } from '../services/businessService'
import { getApiError } from '../services/apiClient'
import { BUSINESS_CATEGORIES } from '../constants/categories'
import { MonthCalendar } from '../components/MonthCalendar'
import type { BusinessHoliday, BusinessHour, BusinessResponse, ServiceResponse, StaffMember } from '../types/api'

// ─── helpers ────────────────────────────────────────────────────────────────

function formatPrice(price: number | null): string {
  if (price === null) return 'Gratis'
  return new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR' }).format(price)
}

/**
 * Sección colapsable de Configuración: cabecera clicable (título + icono + chevron)
 * que despliega/oculta su contenido. Por defecto colapsada (la pantalla queda
 * compacta) salvo que se pase defaultOpen. El contenido se desmonta al cerrar,
 * así que cada sección solo carga sus datos cuando se abre.
 */
function SectionCard({
  id,
  title,
  icon,
  defaultOpen = false,
  children,
}: {
  id: string
  title: string
  icon: string
  defaultOpen?: boolean
  children: React.ReactNode
}) {
  const [open, setOpen] = useState(defaultOpen)
  return (
    <div>
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        data-testid={`section-toggle-${id}`}
        aria-expanded={open}
        className="flex w-full items-center gap-stack-sm p-stack-md text-left transition-colors hover:bg-surface-container-low"
      >
        <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-primary-container/15 text-primary">
          <span className="material-symbols-outlined text-[20px]">{icon}</span>
        </span>
        <h2 className="!mt-0 flex-1 text-base font-bold">{title}</h2>
        <span className={`material-symbols-outlined text-on-surface-variant transition-transform ${open ? 'rotate-180' : ''}`}>
          expand_more
        </span>
      </button>
      {open && (
        <div className="flex flex-col gap-stack-md border-t border-outline-variant/30 p-stack-md" data-testid={`section-body-${id}`}>
          {children}
        </div>
      )}
    </div>
  )
}

// ─── Horario (inline) ────────────────────────────────────────────────────────

const DAYS: ReadonlyArray<{ dayOfWeek: number; label: string }> = [
  { dayOfWeek: 1, label: 'Lunes' },
  { dayOfWeek: 2, label: 'Martes' },
  { dayOfWeek: 3, label: 'Miércoles' },
  { dayOfWeek: 4, label: 'Jueves' },
  { dayOfWeek: 5, label: 'Viernes' },
  { dayOfWeek: 6, label: 'Sábado' },
  { dayOfWeek: 0, label: 'Domingo' },
]

interface DayRow {
  dayOfWeek: number
  label: string
  isClosed: boolean
  opening: string
  closing: string
}

function toInput(time: string | null, fallback: string): string {
  return time ? time.slice(0, 5) : fallback
}

function buildRows(saved: BusinessHour[]): DayRow[] {
  const byDay = new Map(saved.map((h) => [h.dayOfWeek, h]))
  return DAYS.map(({ dayOfWeek, label }) => {
    const h = byDay.get(dayOfWeek)
    if (h) return { dayOfWeek, label, isClosed: h.isClosed, opening: toInput(h.openingTime, '09:00'), closing: toInput(h.closingTime, '17:00') }
    const weekday = dayOfWeek >= 1 && dayOfWeek <= 5
    return { dayOfWeek, label, isClosed: !weekday, opening: '09:00', closing: '17:00' }
  })
}

function HoursSection({ businessId }: { businessId: string }) {
  const [rows, setRows] = useState<DayRow[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const [saved, setSaved] = useState(false)

  useEffect(() => {
    let active = true
    businessService.getHours(businessId)
      .then((h) => { if (active) setRows(buildRows(h)) })
      .catch((err) => { if (active) setError(getApiError(err)?.message ?? 'No se pudo cargar el horario.') })
    return () => { active = false }
  }, [businessId])

  function updateRow(dayOfWeek: number, patch: Partial<DayRow>) {
    setRows((prev) => prev?.map((r) => (r.dayOfWeek === dayOfWeek ? { ...r, ...patch } : r)) ?? prev)
    setSaved(false)
  }

  async function handleSave(e: FormEvent) {
    e.preventDefault()
    if (!rows) return
    const invalid = rows.find((r) => !r.isClosed && r.opening >= r.closing)
    if (invalid) { setError(`En ${invalid.label} la apertura debe ser anterior al cierre.`); return }
    setError(null)
    setSaving(true)
    try {
      const days: BusinessHour[] = rows.map((r) => ({
        dayOfWeek: r.dayOfWeek,
        isClosed: r.isClosed,
        openingTime: r.isClosed ? null : `${r.opening}:00`,
        closingTime: r.isClosed ? null : `${r.closing}:00`,
      }))
      const updated = await businessService.setHours(businessId, days)
      setRows(buildRows(updated))
      setSaved(true)
    } catch (err) {
      setError(getApiError(err)?.message ?? 'No se pudo guardar el horario.')
    } finally {
      setSaving(false)
    }
  }

  if (error) return <p role="alert" className="alert" data-testid="hours-error">{error}</p>
  if (!rows) return <p className="text-on-surface-variant text-sm">Cargando…</p>

  return (
    <form onSubmit={handleSave} data-testid="hours-form">
      <ul className="flex flex-col gap-2 mb-stack-md">
        {rows.map((r) => (
          <li key={r.dayOfWeek} className="glass-card rounded-xl px-stack-md py-3 flex items-center gap-stack-md flex-wrap" data-testid={`hours-day-${r.dayOfWeek}`}>
            <span className="font-semibold w-24 text-sm">{r.label}</span>
            <label className="inline-flex items-center gap-2 text-sm font-medium cursor-pointer">
              <input type="checkbox" className="w-4 h-4 accent-primary-container"
                data-testid={`hours-day-${r.dayOfWeek}-open-toggle`}
                checked={!r.isClosed}
                onChange={(e) => updateRow(r.dayOfWeek, { isClosed: !e.target.checked })} />
              {r.isClosed ? 'Cerrado' : 'Abierto'}
            </label>
            {!r.isClosed && (
              <span className="flex items-center gap-2 ml-auto">
                <input type="time" className="field-input !py-1.5 w-28" data-testid={`hours-day-${r.dayOfWeek}-opening`}
                  value={r.opening} onChange={(e) => updateRow(r.dayOfWeek, { opening: e.target.value })} required />
                <span className="text-on-surface-variant">–</span>
                <input type="time" className="field-input !py-1.5 w-28" data-testid={`hours-day-${r.dayOfWeek}-closing`}
                  value={r.closing} onChange={(e) => updateRow(r.dayOfWeek, { closing: e.target.value })} required />
              </span>
            )}
          </li>
        ))}
      </ul>
      <div className="flex items-center gap-stack-md">
        <button type="submit" className="btn-primary" data-testid="hours-save" disabled={saving}>
          {saving ? 'Guardando…' : 'Guardar horario'}
        </button>
        {saved && (
          <p className="inline-flex items-center gap-1 text-sm font-semibold text-secondary" data-testid="hours-saved">
            <span className="material-symbols-outlined text-[18px]">check_circle</span>
            Guardado.
          </p>
        )}
      </div>
    </form>
  )
}

// ─── Festivos ────────────────────────────────────────────────────────────────

function hhmm(time: string | null): string {
  return time ? time.slice(0, 5) : ''
}

/** Texto legible de un festivo: rango de días y/o franja horaria. */
function holidayLabel(h: BusinessHoliday): string {
  const days = h.endDate && h.endDate !== h.holidayDate ? `${h.holidayDate} → ${h.endDate}` : h.holidayDate
  const hours = h.startTime && h.endTime ? ` · ${hhmm(h.startTime)}–${hhmm(h.endTime)}` : ' · todo el día'
  return days + hours
}

function HolidaysSection({ businessId }: { businessId: string }) {
  const [holidays, setHolidays] = useState<BusinessHoliday[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [date, setDate] = useState('')
  const [endDate, setEndDate] = useState('')
  const [partial, setPartial] = useState(false)
  const [startTime, setStartTime] = useState('09:00')
  const [endTime, setEndTime] = useState('14:00')
  const [reason, setReason] = useState('')
  const [adding, setAdding] = useState(false)
  const [addError, setAddError] = useState<string | null>(null)

  const load = useCallback(async () => {
    try {
      setHolidays(await businessService.getHolidays(businessId))
    } catch (err) {
      setError(getApiError(err)?.message ?? 'No se pudieron cargar los festivos.')
    }
  }, [businessId])

  useEffect(() => { load() }, [load])

  async function handleAdd(e: FormEvent) {
    e.preventDefault()
    if (!date) return
    if (partial && startTime >= endTime) { setAddError('La hora de inicio debe ser anterior a la de fin.'); return }
    setAddError(null)
    setAdding(true)
    try {
      await businessService.addHoliday(businessId, {
        holidayDate: date,
        reason: reason.trim() || null,
        endDate: endDate && endDate !== date ? endDate : null,
        startTime: partial ? `${startTime}:00` : null,
        endTime: partial ? `${endTime}:00` : null,
      })
      setDate(''); setEndDate(''); setReason(''); setPartial(false)
      await load()
    } catch (err) {
      setAddError(getApiError(err)?.message ?? 'No se pudo añadir el festivo.')
    } finally {
      setAdding(false)
    }
  }

  async function handleRemove(id: string) {
    try {
      await businessService.removeHoliday(businessId, id)
      setHolidays((prev) => prev?.filter((h) => h.id !== id) ?? null)
    } catch (err) {
      setError(getApiError(err)?.message ?? 'No se pudo eliminar.')
    }
  }

  if (error) return <p role="alert" className="alert">{error}</p>

  const today = new Date().toISOString().slice(0, 10)

  return (
    <div className="flex flex-col gap-stack-md">
      {holidays !== null && holidays.length > 0 && (
        <ul className="flex flex-col gap-2" data-testid="holidays-list">
          {holidays
            .slice()
            .sort((a, b) => a.holidayDate.localeCompare(b.holidayDate))
            .map((h) => (
              <li key={h.id} className="glass-card rounded-xl px-stack-md py-3 flex items-center gap-stack-md" data-testid="holiday-item">
                <span className="material-symbols-outlined text-[20px] text-on-surface-variant">beach_access</span>
                <div className="flex-1 min-w-0">
                  <p className="font-semibold text-sm">{holidayLabel(h)}</p>
                  {h.reason && <p className="text-xs text-on-surface-variant">{h.reason}</p>}
                </div>
                <button
                  type="button"
                  onClick={() => handleRemove(h.id)}
                  className="p-1 rounded-lg text-error hover:bg-error-container/30 transition-colors"
                  aria-label="Eliminar festivo"
                >
                  <span className="material-symbols-outlined text-[18px]">delete</span>
                </button>
              </li>
            ))}
        </ul>
      )}
      {holidays !== null && holidays.length === 0 && (
        <p className="text-sm text-on-surface-variant">No hay festivos configurados.</p>
      )}
      {holidays === null && <p className="text-sm text-on-surface-variant">Cargando…</p>}

      <form onSubmit={handleAdd} className="flex flex-col gap-stack-sm border-t border-outline-variant/30 pt-stack-md" data-testid="add-holiday-form">
        <p className="text-sm font-semibold">Añadir cierre</p>
        {addError && <p role="alert" className="alert text-xs" data-testid="holiday-error">{addError}</p>}
        {/* Calendarios compactos lado a lado (apilados en móvil) para elegir Desde/Hasta. */}
        <div className="flex flex-wrap gap-stack-md items-start">
          <div className="field !gap-1" data-testid="holiday-date-field">
            <label className="field-label text-xs">Desde</label>
            <MonthCalendar value={date} min={today} onSelect={(d) => setDate(d)} />
          </div>
          <div className="field !gap-1" data-testid="holiday-end-date-field">
            <div className="flex items-center justify-between gap-2">
              <label className="field-label text-xs">Hasta (opcional)</label>
              {endDate && (
                <button type="button" className="text-xs text-primary hover:underline"
                  onClick={() => setEndDate('')} data-testid="holiday-end-date-clear">Quitar</button>
              )}
            </div>
            {/* El fin no puede ser anterior al inicio; si no hay inicio, mínimo = hoy. */}
            <MonthCalendar value={endDate} min={date || today} onSelect={(d) => setEndDate(d)} />
          </div>
        </div>
        <div className="field !gap-1 flex-1 min-w-40">
          <label className="field-label text-xs" htmlFor="holiday-reason">Motivo (opcional)</label>
          <input id="holiday-reason" type="text" className="field-input !py-2"
            value={reason} onChange={(e) => setReason(e.target.value)} placeholder="Navidad, vacaciones…" />
        </div>

        <label className="inline-flex items-center gap-2 text-sm font-medium cursor-pointer">
          <input type="checkbox" className="w-4 h-4 accent-primary-container" checked={partial}
            onChange={(e) => setPartial(e.target.checked)} data-testid="holiday-partial-toggle" />
          Cerrar solo unas horas (si no, se cierra el día completo)
        </label>
        {partial && (
          <div className="flex items-center gap-2">
            <input type="time" className="field-input !py-1.5 w-28" value={startTime}
              onChange={(e) => setStartTime(e.target.value)} data-testid="holiday-start-time" aria-label="Hora de inicio" />
            <span className="text-on-surface-variant">–</span>
            <input type="time" className="field-input !py-1.5 w-28" value={endTime}
              onChange={(e) => setEndTime(e.target.value)} data-testid="holiday-end-time" aria-label="Hora de fin" />
          </div>
        )}

        <button type="submit" className="btn-primary !py-2 text-sm self-start" disabled={adding} data-testid="add-holiday-submit">
          {adding ? 'Añadiendo…' : 'Añadir'}
        </button>
      </form>
    </div>
  )
}

// ─── Equipo (trabajadores) ───────────────────────────────────────────────────

function roleLabel(role: string): string {
  return role === 'owner' ? 'Propietario' : 'Empleado'
}

/** Editor de qué servicios realiza un trabajador. Sin ninguno marcado = realiza todos. */
function StaffServicesEditor({ businessId, staffId, services }: { businessId: string; staffId: string; services: ServiceResponse[] }) {
  const [selected, setSelected] = useState<Set<string> | null>(null)
  const [saving, setSaving] = useState(false)
  const [saved, setSaved] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    businessService.getStaffServices(businessId, staffId)
      .then((ids) => {
        if (!active) return
        // Sin asignaciones = realiza todos: marcamos todos por defecto.
        setSelected(new Set(ids.length > 0 ? ids : services.map((s) => s.id)))
      })
      .catch((err) => { if (active) setError(getApiError(err)?.message ?? 'No se pudieron cargar los servicios.') })
    return () => { active = false }
  }, [businessId, staffId, services])

  function toggle(id: string) {
    setSelected((prev) => {
      if (!prev) return prev
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
    setSaved(false)
  }

  async function handleSave() {
    if (!selected) return
    setSaving(true)
    setError(null)
    try {
      await businessService.setStaffServices(businessId, staffId, [...selected])
      setSaved(true)
    } catch (err) {
      setError(getApiError(err)?.message ?? 'No se pudo guardar.')
    } finally {
      setSaving(false)
    }
  }

  if (error) return <p role="alert" className="alert text-xs">{error}</p>
  if (!selected) return <p className="text-xs text-on-surface-variant">Cargando servicios…</p>
  if (services.length === 0) return <p className="text-xs text-on-surface-variant">Crea servicios primero para poder asignarlos.</p>

  return (
    <div className="flex flex-col gap-2" data-testid="staff-services-editor">
      <p className="text-xs text-on-surface-variant">
        Marca los servicios que realiza. Si no marcas ninguno, podrá realizar <strong>todos</strong>.
      </p>
      <div className="flex flex-wrap gap-2">
        {services.map((svc) => {
          const on = selected.has(svc.id)
          return (
            <label
              key={svc.id}
              className={`inline-flex items-center gap-1.5 rounded-full border px-3 py-1.5 text-xs font-medium cursor-pointer transition-colors ${
                on ? 'border-primary bg-primary-container/20 text-primary' : 'border-outline-variant text-on-surface-variant hover:bg-surface-container-low'
              }`}
            >
              <input type="checkbox" className="sr-only" checked={on} onChange={() => toggle(svc.id)} data-testid="staff-service-toggle" data-service-id={svc.id} />
              <span className="material-symbols-outlined text-[16px]">{on ? 'check_circle' : 'radio_button_unchecked'}</span>
              {svc.name}
            </label>
          )
        })}
      </div>
      <div className="flex items-center gap-stack-md">
        <button type="button" className="btn-primary !py-1.5 text-xs" onClick={handleSave} disabled={saving} data-testid="staff-services-save">
          {saving ? 'Guardando…' : 'Guardar servicios'}
        </button>
        {saved && (
          <span className="inline-flex items-center gap-1 text-xs font-semibold text-secondary">
            <span className="material-symbols-outlined text-[14px]">check_circle</span> Guardado
          </span>
        )}
      </div>
    </div>
  )
}

/** Una fila de servicio con edición inline y borrado. */
function ServiceRow({ businessId, service, onChanged }: { businessId: string; service: ServiceResponse; onChanged: () => void }) {
  const [editing, setEditing] = useState(false)
  const [name, setName] = useState(service.name)
  const [duration, setDuration] = useState(String(service.durationMinutes))
  const [price, setPrice] = useState(service.price === null ? '' : String(service.price))
  const [description, setDescription] = useState(service.description ?? '')
  const [color, setColor] = useState(service.color ?? '#7C3AED')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function handleSave(e: FormEvent) {
    e.preventDefault()
    setSaving(true)
    setError(null)
    try {
      await businessService.updateService(businessId, service.id, {
        name: name.trim(),
        description: description.trim() || null,
        durationMinutes: Number(duration),
        price: price.trim() === '' ? null : Number(price),
        color: color || null,
      })
      setEditing(false)
      onChanged()
    } catch (err) {
      setError(getApiError(err)?.message ?? 'No se pudo guardar el servicio.')
    } finally {
      setSaving(false)
    }
  }

  async function handleDelete() {
    if (!window.confirm(`¿Eliminar el servicio «${service.name}»?`)) return
    try {
      await businessService.deleteService(businessId, service.id)
      onChanged()
    } catch (err) {
      setError(getApiError(err)?.message ?? 'No se pudo eliminar.')
    }
  }

  if (editing) {
    return (
      <li className="glass-card rounded-xl p-stack-md" data-testid="service-item">
        <form onSubmit={handleSave} className="flex flex-col gap-stack-sm" data-testid="edit-service-form">
          {error && <p role="alert" className="alert text-xs">{error}</p>}
          <input type="text" className="field-input !py-2" data-testid="edit-service-name"
            value={name} onChange={(e) => setName(e.target.value)} placeholder="Nombre" required />
          <div className="grid grid-cols-2 gap-2">
            <input type="number" className="field-input !py-2" data-testid="edit-service-duration" aria-label="Duración (min)"
              value={duration} onChange={(e) => setDuration(e.target.value)} min={5} step={5} required />
            <input type="number" className="field-input !py-2" data-testid="edit-service-price" aria-label="Precio (€)"
              value={price} onChange={(e) => setPrice(e.target.value)} min={0} step="0.01" placeholder="€ (vacío = gratis)" />
          </div>
          <input type="text" className="field-input !py-2" data-testid="edit-service-description"
            value={description} onChange={(e) => setDescription(e.target.value)} placeholder="Descripción (opcional)" />
          <div className="flex items-center gap-2">
            <input type="color" className="h-9 w-12 rounded-lg border border-outline-variant bg-surface-container-lowest p-1" data-testid="edit-service-color"
              value={color} onChange={(e) => setColor(e.target.value)} aria-label="Color" />
            <button type="submit" className="btn-primary !py-1.5 text-sm" data-testid="edit-service-save" disabled={saving}>
              {saving ? 'Guardando…' : 'Guardar'}
            </button>
            <button type="button" className="text-sm font-semibold text-on-surface-variant hover:underline" onClick={() => setEditing(false)}>
              Cancelar
            </button>
          </div>
        </form>
      </li>
    )
  }

  return (
    <li className="glass-card rounded-xl p-stack-md flex items-center gap-stack-md" data-testid="service-item">
      <span className="w-3.5 h-3.5 rounded-full shrink-0 ring-1 ring-black/10" style={{ background: service.color ?? '#cbd5e1' }} aria-hidden />
      <div className="flex-1 min-w-0">
        <strong className="font-semibold text-sm">{service.name}</strong>
        <p className="text-xs text-on-surface-variant">
          {service.durationMinutes} min · {formatPrice(service.price)}
          {service.description ? ` · ${service.description}` : ''}
        </p>
        {error && <p role="alert" className="text-xs text-error mt-1">{error}</p>}
      </div>
      <button type="button" onClick={() => setEditing(true)}
        className="p-1 rounded-lg text-on-surface-variant hover:bg-surface-container-low transition-colors"
        aria-label={`Editar ${service.name}`} data-testid="edit-service-btn">
        <span className="material-symbols-outlined text-[18px]">edit</span>
      </button>
      <button type="button" onClick={handleDelete}
        className="p-1 rounded-lg text-error hover:bg-error-container/30 transition-colors"
        aria-label={`Eliminar ${service.name}`} data-testid="delete-service-btn">
        <span className="material-symbols-outlined text-[18px]">delete</span>
      </button>
    </li>
  )
}

function TeamSection({ businessId, services }: { businessId: string; services: ServiceResponse[] | null }) {
  const [staff, setStaff] = useState<StaffMember[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [phone, setPhone] = useState('')
  const [adding, setAdding] = useState(false)
  const [addError, setAddError] = useState<string | null>(null)
  const [premiumRequired, setPremiumRequired] = useState(false)
  const [expandedId, setExpandedId] = useState<string | null>(null)
  // Enlace de invitación generado para un empleado (email simulado → el owner lo copia).
  const [inviteLink, setInviteLink] = useState<{ staffId: string; url: string } | null>(null)
  const [inviteError, setInviteError] = useState<string | null>(null)

  async function handleInvite(member: StaffMember) {
    setInviteError(null)
    try {
      const res = await businessService.inviteStaff(businessId, member.id)
      setInviteLink({ staffId: member.id, url: `${window.location.origin}/invitacion/${res.token}` })
    } catch (err) {
      setInviteLink(null)
      setInviteError(getApiError(err)?.message ?? 'No se pudo generar la invitación.')
    }
  }

  const load = useCallback(async () => {
    try {
      setStaff(await businessService.listStaff(businessId))
    } catch (err) {
      setError(getApiError(err)?.message ?? 'No se pudo cargar el equipo.')
    }
  }, [businessId])

  useEffect(() => { load() }, [load])

  async function handleAdd(e: FormEvent) {
    e.preventDefault()
    if (!name.trim()) return
    setAddError(null)
    setPremiumRequired(false)
    setAdding(true)
    try {
      await businessService.createStaff(businessId, { name: name.trim(), email: email.trim() || null, phone: phone.trim() || null })
      setName(''); setEmail(''); setPhone('')
      await load()
    } catch (err) {
      const apiErr = getApiError(err)
      if (apiErr?.error === 'limit_reached') {
        setPremiumRequired(true)
      } else {
        setAddError(apiErr?.message ?? 'No se pudo añadir el empleado.')
      }
    } finally {
      setAdding(false)
    }
  }

  async function handleRemove(member: StaffMember) {
    if (!window.confirm(`¿Dar de baja a ${member.name}?`)) return
    try {
      await businessService.deactivateStaff(businessId, member.id)
      setStaff((prev) => prev?.filter((s) => s.id !== member.id) ?? null)
    } catch (err) {
      setError(getApiError(err)?.message ?? 'No se pudo dar de baja.')
    }
  }

  if (error) return <p role="alert" className="alert" data-testid="staff-error">{error}</p>

  return (
    <div className="flex flex-col gap-stack-md">
      {inviteError && <p role="alert" className="alert text-xs" data-testid="staff-invite-error">{inviteError}</p>}
      {staff === null && <p className="text-sm text-on-surface-variant">Cargando…</p>}
      {staff !== null && (
        <ul className="flex flex-col gap-2" data-testid="staff-list">
          {staff.map((member) => (
            <li key={member.id} className="glass-card rounded-xl px-stack-md py-3 flex flex-col gap-stack-sm" data-testid="staff-item">
              <div className="flex items-center gap-stack-md">
                <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-primary-container/20 text-sm font-bold text-primary">
                  {member.name[0]?.toUpperCase() ?? '?'}
                </span>
                <div className="flex-1 min-w-0">
                  <p className="font-semibold text-sm truncate" data-testid="staff-name">{member.name}</p>
                  <span className={`inline-block rounded-full px-2 py-0.5 text-[11px] font-semibold ${
                    member.role === 'owner' ? 'bg-primary-container/30 text-primary' : 'bg-surface-container text-on-surface-variant'
                  }`}>
                    {roleLabel(member.role)}
                  </span>
                </div>
                <button
                  type="button"
                  onClick={() => setExpandedId((id) => (id === member.id ? null : member.id))}
                  className="inline-flex items-center gap-1 rounded-lg px-2 py-1 text-xs font-semibold text-on-surface-variant hover:bg-surface-container-low transition-colors"
                  data-testid="staff-services-btn"
                  aria-expanded={expandedId === member.id}
                >
                  <span className="material-symbols-outlined text-[18px]">content_cut</span>
                  Servicios
                </button>
                {member.role !== 'owner' && (
                  <>
                    <button
                      type="button"
                      onClick={() => handleInvite(member)}
                      className="inline-flex items-center gap-1 rounded-lg px-2 py-1 text-xs font-semibold text-primary hover:bg-primary-container/15 transition-colors"
                      data-testid="staff-invite-btn"
                    >
                      <span className="material-symbols-outlined text-[18px]">mail</span>
                      Invitar
                    </button>
                    <button
                      type="button"
                      onClick={() => handleRemove(member)}
                      className="p-1 rounded-lg text-error hover:bg-error-container/30 transition-colors"
                      aria-label={`Dar de baja a ${member.name}`}
                      data-testid="staff-remove"
                    >
                      <span className="material-symbols-outlined text-[18px]">person_remove</span>
                    </button>
                  </>
                )}
              </div>
              {inviteLink?.staffId === member.id && (
                <div className="border-t border-outline-variant/30 pt-stack-sm flex flex-col gap-1" data-testid="staff-invite-link">
                  <p className="text-xs text-on-surface-variant">Pásale este enlace para que cree su cuenta (el email es simulado):</p>
                  <div className="flex items-center gap-2">
                    <input readOnly className="field-input !py-1.5 flex-1 text-xs" value={inviteLink.url} data-testid="staff-invite-url"
                      onFocus={(e) => e.currentTarget.select()} />
                    <button type="button" className="btn-primary !py-1.5 text-xs shrink-0"
                      onClick={() => navigator.clipboard?.writeText(inviteLink.url)}>Copiar</button>
                  </div>
                </div>
              )}
              {expandedId === member.id && services !== null && (
                <div className="border-t border-outline-variant/30 pt-stack-sm">
                  <StaffServicesEditor businessId={businessId} staffId={member.id} services={services} />
                </div>
              )}
            </li>
          ))}
        </ul>
      )}

      <form onSubmit={handleAdd} className="flex flex-col gap-stack-sm border-t border-outline-variant/30 pt-stack-md" data-testid="create-staff-form">
        <p className="text-sm font-semibold">Añadir empleado</p>
        {addError && <p role="alert" className="alert text-xs" data-testid="create-staff-error">{addError}</p>}
        {premiumRequired && (
          <div className="rounded-xl border border-primary/30 bg-primary-container/10 px-stack-md py-3 text-sm" data-testid="staff-premium-required">
            <p className="font-semibold text-primary">Añadir empleados requiere el plan Premium</p>
            <p className="text-on-surface-variant text-xs mt-1">
              El plan Free incluye solo al propietario. Podrás mejorar a Premium desde la sección «Plan» (próximamente).
            </p>
          </div>
        )}
        <div className="flex flex-wrap gap-2 items-end">
          <div className="field !gap-1 flex-1 min-w-40">
            <label className="field-label text-xs" htmlFor="staff-name">Nombre</label>
            <input id="staff-name" type="text" className="field-input !py-2" data-testid="staff-name-input"
              value={name} onChange={(e) => setName(e.target.value)} placeholder="Ana García" required />
          </div>
          <div className="field !gap-1 min-w-40">
            <label className="field-label text-xs" htmlFor="staff-email">Email (opcional)</label>
            <input id="staff-email" type="email" className="field-input !py-2" data-testid="staff-email-input"
              value={email} onChange={(e) => setEmail(e.target.value)} placeholder="ana@negocio.com" />
          </div>
          <div className="field !gap-1 min-w-36">
            <label className="field-label text-xs" htmlFor="staff-phone">Teléfono (opcional)</label>
            <input id="staff-phone" type="tel" className="field-input !py-2" data-testid="staff-phone-input"
              value={phone} onChange={(e) => setPhone(e.target.value)} placeholder="+34 600 000 000" />
          </div>
          <button type="submit" className="btn-primary !py-2 text-sm" data-testid="create-staff-submit" disabled={adding}>
            {adding ? 'Añadiendo…' : 'Añadir'}
          </button>
        </div>
      </form>
    </div>
  )
}

// ─── Perfil público (Explorar) ───────────────────────────────────────────────

function ProfileSection({ businessId, business, onUpdated }: { businessId: string; business: BusinessResponse | null; onUpdated: (b: BusinessResponse) => void }) {
  const [category, setCategory] = useState('')
  const [photoUrl, setPhotoUrl] = useState('')
  const [lat, setLat] = useState('')
  const [lng, setLng] = useState('')
  const [saving, setSaving] = useState(false)
  const [saved, setSaved] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [locating, setLocating] = useState(false)

  useEffect(() => {
    if (!business) return
    setCategory(business.category ?? '')
    setPhotoUrl(business.photoUrl ?? '')
    setLat(business.latitude != null ? String(business.latitude) : '')
    setLng(business.longitude != null ? String(business.longitude) : '')
  }, [business])

  function useMyLocation() {
    if (!navigator.geolocation) { setError('Tu navegador no permite geolocalización.'); return }
    setLocating(true)
    navigator.geolocation.getCurrentPosition(
      (pos) => { setLat(pos.coords.latitude.toFixed(6)); setLng(pos.coords.longitude.toFixed(6)); setLocating(false); setSaved(false) },
      () => { setError('No se pudo obtener tu ubicación.'); setLocating(false) },
      { timeout: 8000 },
    )
  }

  async function handleSave(e: FormEvent) {
    e.preventDefault()
    setSaving(true)
    setError(null)
    try {
      const updated = await businessService.updateProfile(businessId, {
        category: category || null,
        photoUrl: photoUrl.trim() || null,
        latitude: lat.trim() === '' ? null : Number(lat),
        longitude: lng.trim() === '' ? null : Number(lng),
      })
      onUpdated(updated)
      setSaved(true)
    } catch (err) {
      setError(getApiError(err)?.message ?? 'No se pudo guardar el perfil.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <form onSubmit={handleSave} className="flex flex-col gap-stack-md" data-testid="profile-form">
      {error && <p role="alert" className="alert text-sm">{error}</p>}
      <div className="field">
        <label className="field-label" htmlFor="profile-category">Categoría</label>
        <select id="profile-category" className="field-input" data-testid="profile-category"
          value={category} onChange={(e) => { setCategory(e.target.value); setSaved(false) }}>
          <option value="">Sin categoría</option>
          {BUSINESS_CATEGORIES.map((c) => (
            <option key={c.code} value={c.code}>{c.label}</option>
          ))}
        </select>
      </div>
      <div className="field">
        <label className="field-label" htmlFor="profile-photo">Foto (URL)</label>
        <input id="profile-photo" type="url" className="field-input" data-testid="profile-photo"
          value={photoUrl} onChange={(e) => { setPhotoUrl(e.target.value); setSaved(false) }} placeholder="https://…/foto.jpg" />
        {photoUrl.trim() && (
          <img src={photoUrl} alt="Vista previa" className="mt-2 h-24 w-full max-w-xs rounded-xl object-cover"
            onError={(e) => { (e.currentTarget as HTMLImageElement).style.display = 'none' }} />
        )}
      </div>
      <div className="field">
        <label className="field-label">Ubicación (para "negocios cercanos")</label>
        <div className="flex flex-wrap items-end gap-2">
          <input type="number" step="any" className="field-input w-36" data-testid="profile-lat" aria-label="Latitud"
            value={lat} onChange={(e) => { setLat(e.target.value); setSaved(false) }} placeholder="Latitud" />
          <input type="number" step="any" className="field-input w-36" data-testid="profile-lng" aria-label="Longitud"
            value={lng} onChange={(e) => { setLng(e.target.value); setSaved(false) }} placeholder="Longitud" />
          <button type="button" onClick={useMyLocation} disabled={locating} data-testid="profile-locate"
            className="btn-secondary !py-2 text-sm inline-flex items-center gap-1">
            <span className="material-symbols-outlined text-[18px]">my_location</span>
            {locating ? 'Localizando…' : 'Usar mi ubicación'}
          </button>
        </div>
      </div>
      <div className="flex items-center gap-stack-md">
        <button type="submit" className="btn-primary self-start" data-testid="profile-save" disabled={saving}>
          {saving ? 'Guardando…' : 'Guardar perfil'}
        </button>
        {saved && (
          <span className="inline-flex items-center gap-1 text-sm font-semibold text-secondary">
            <span className="material-symbols-outlined text-[16px]">check_circle</span> Guardado
          </span>
        )}
      </div>
    </form>
  )
}

// ─── Avisos a clientes (notificaciones) ──────────────────────────────────────

function NotificationsSection({ businessId, business, onUpdated }: { businessId: string; business: BusinessResponse | null; onUpdated: (b: BusinessResponse) => void }) {
  const [email, setEmail] = useState(true)
  const [whatsapp, setWhatsapp] = useState(false)
  const [reminderHours, setReminderHours] = useState('24')
  const [saving, setSaving] = useState(false)
  const [saved, setSaved] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!business) return
    setEmail(business.notifyByEmail)
    setWhatsapp(business.notifyByWhatsapp)
    setReminderHours(String(business.reminderHoursBefore))
  }, [business])

  async function handleSave(e: FormEvent) {
    e.preventDefault()
    setError(null)
    setSaving(true)
    try {
      const updated = await businessService.setNotificationSettings(businessId, {
        notifyByEmail: email,
        notifyByWhatsapp: whatsapp,
        reminderHoursBefore: Number(reminderHours) || 0,
      })
      onUpdated(updated)
      setSaved(true)
    } catch (err) {
      setError(getApiError(err)?.message ?? 'No se pudo guardar la configuración de avisos.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <form onSubmit={handleSave} className="flex flex-col gap-stack-md" data-testid="notifications-form">
      <p className="text-sm text-on-surface-variant -mt-stack-sm">
        Avisa a tus clientes cuando su reserva se crea, se reprograma o se cancela, y envíales un recordatorio antes de la cita.
      </p>
      {error && <p role="alert" className="alert text-sm" data-testid="notifications-error">{error}</p>}

      <label className="inline-flex items-center gap-2 text-sm font-medium cursor-pointer">
        <input type="checkbox" className="w-4 h-4 accent-primary-container" data-testid="notify-email"
          checked={email} onChange={(e) => { setEmail(e.target.checked); setSaved(false) }} />
        <span className="material-symbols-outlined text-[18px] text-on-surface-variant">mail</span>
        Avisos por email
      </label>
      <label className="inline-flex items-center gap-2 text-sm font-medium cursor-pointer">
        <input type="checkbox" className="w-4 h-4 accent-primary-container" data-testid="notify-whatsapp"
          checked={whatsapp} onChange={(e) => { setWhatsapp(e.target.checked); setSaved(false) }} />
        <span className="material-symbols-outlined text-[18px] text-on-surface-variant">chat</span>
        Avisos por WhatsApp
      </label>

      <div className="field !gap-1">
        <label className="field-label text-xs" htmlFor="reminder-hours">Recordatorio: horas antes de la cita (0 = sin recordatorio)</label>
        <div className="flex items-center gap-2">
          <input id="reminder-hours" type="number" className="field-input w-28" data-testid="reminder-hours"
            value={reminderHours} onChange={(e) => { setReminderHours(e.target.value); setSaved(false) }} min={0} max={168} step={1} />
          <span className="text-sm text-on-surface-variant">horas</span>
        </div>
      </div>

      <p className="inline-flex items-center gap-1 text-xs text-on-surface-variant">
        <span className="material-symbols-outlined text-[16px]">info</span>
        En esta versión los avisos se simulan y quedan registrados (demo); el envío real de email/WhatsApp se conecta sin cambiar la app.
      </p>

      <div className="flex items-center gap-stack-md">
        <button type="submit" className="btn-primary self-start" data-testid="notifications-save" disabled={saving}>
          {saving ? 'Guardando…' : 'Guardar avisos'}
        </button>
        {saved && (
          <span className="inline-flex items-center gap-1 text-sm font-semibold text-secondary">
            <span className="material-symbols-outlined text-[16px]">check_circle</span> Guardado
          </span>
        )}
      </div>
    </form>
  )
}

// ─── Página principal ────────────────────────────────────────────────────────

export function BusinessSettingsPage() {
  const { businessId, isOwner } = useAuth()

  const [business, setBusiness] = useState<BusinessResponse | null>(null)
  const [services, setServices] = useState<ServiceResponse[] | null>(null)
  const [staffCount, setStaffCount] = useState<number | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [linkCopied, setLinkCopied] = useState(false)

  // Servicios — form
  const [svcName, setSvcName] = useState('')
  const [svcDuration, setSvcDuration] = useState('30')
  const [svcPrice, setSvcPrice] = useState('')
  const [svcDescription, setSvcDescription] = useState('')
  const [svcColor, setSvcColor] = useState('#7C3AED')
  const [svcSaving, setSvcSaving] = useState(false)
  const [svcFormError, setSvcFormError] = useState<string | null>(null)
  const [svcFormOpen, setSvcFormOpen] = useState(false)

  // Confirmación
  const [confSaving, setConfSaving] = useState(false)
  const [confSaved, setConfSaved] = useState(false)
  const [confError, setConfError] = useState<string | null>(null)

  // Modo de reservas (online | calendar_only)
  const [bookingModeSaving, setBookingModeSaving] = useState(false)
  const [bookingModeError, setBookingModeError] = useState<string | null>(null)

  // Ventana de cancelación
  const [cutoffHours, setCutoffHours] = useState('')
  const [cutoffSaving, setCutoffSaving] = useState(false)
  const [cutoffSaved, setCutoffSaved] = useState(false)
  const [cutoffError, setCutoffError] = useState<string | null>(null)

  // Plan
  const [planSaving, setPlanSaving] = useState(false)
  const [planError, setPlanError] = useState<string | null>(null)

  const loadServices = useCallback(async (id: string) => {
    try {
      setServices(await businessService.listServices(id))
    } catch (err) {
      setLoadError(getApiError(err)?.message ?? 'No se pudieron cargar los servicios.')
    }
  }, [])

  useEffect(() => {
    if (!businessId) return
    let active = true
    businessService.listMine()
      .then((list) => {
        if (!active) return
        const b = list.find((x) => x.id === businessId) ?? list[0] ?? null
        setBusiness(b)
        if (b) {
          setCutoffHours(String(b.cancellationCutoffHours ?? 0))
        }
      })
      .catch((err) => { if (active) setLoadError(getApiError(err)?.message ?? 'No se pudo cargar tu negocio.') })
    loadServices(businessId)
    businessService.listStaff(businessId)
      .then((s) => { if (active) setStaffCount(s.length) })
      .catch(() => { /* el aviso de límites es informativo; si falla, se omite */ })
    return () => { active = false }
  }, [businessId, loadServices])

  async function handleCreateService(e: FormEvent) {
    e.preventDefault()
    if (!businessId) return
    setSvcFormError(null)
    setSvcSaving(true)
    try {
      await businessService.createService(businessId, {
        name: svcName.trim(),
        description: svcDescription.trim() || null,
        durationMinutes: Number(svcDuration),
        price: svcPrice.trim() === '' ? null : Number(svcPrice),
        color: svcColor || null,
      })
      setSvcName(''); setSvcDuration('30'); setSvcPrice(''); setSvcDescription(''); setSvcColor('#7C3AED')
      setSvcFormOpen(false)
      await loadServices(businessId)
    } catch (err) {
      const apiErr = getApiError(err)
      setSvcFormError(
        apiErr?.error === 'limit_reached'
          ? 'Has alcanzado el límite de servicios de tu plan.'
          : apiErr?.message ?? 'No se pudo crear el servicio.',
      )
    } finally {
      setSvcSaving(false)
    }
  }

  async function handleSetMode(mode: string) {
    if (!businessId) return
    setConfError(null)
    setConfSaving(true)
    setConfSaved(false)
    try {
      const updated = await businessService.setConfirmationMode(businessId, mode)
      setBusiness((prev) => prev ? { ...prev, confirmationMode: updated.confirmationMode } : prev)
      setConfSaved(true)
    } catch (err) {
      setConfError(getApiError(err)?.message ?? 'No se pudo cambiar el modo.')
    } finally {
      setConfSaving(false)
    }
  }

  async function handleSetBookingMode(mode: string) {
    if (!businessId) return
    setBookingModeError(null)
    setBookingModeSaving(true)
    try {
      const updated = await businessService.setBookingMode(businessId, mode)
      setBusiness((prev) => prev ? { ...prev, bookingMode: updated.bookingMode } : prev)
    } catch (err) {
      setBookingModeError(getApiError(err)?.message ?? 'No se pudo cambiar el modo de reservas.')
    } finally {
      setBookingModeSaving(false)
    }
  }

  async function handleSaveCutoff(e: FormEvent) {
    e.preventDefault()
    if (!businessId) return
    setCutoffError(null)
    setCutoffSaving(true)
    setCutoffSaved(false)
    try {
      const updated = await businessService.setCancellationCutoff(businessId, Number(cutoffHours))
      setBusiness((prev) => prev ? { ...prev, cancellationCutoffHours: updated.cancellationCutoffHours } : prev)
      setCutoffSaved(true)
    } catch (err) {
      setCutoffError(getApiError(err)?.message ?? 'No se pudo guardar.')
    } finally {
      setCutoffSaving(false)
    }
  }

  async function handleSetPlan(code: string) {
    if (!businessId) return
    setPlanError(null)
    setPlanSaving(true)
    try {
      const updated = await businessService.setPlan(businessId, code)
      setBusiness((prev) => prev ? { ...prev, plan: updated.plan } : prev)
    } catch (err) {
      setPlanError(getApiError(err)?.message ?? 'No se pudo cambiar el plan.')
    } finally {
      setPlanSaving(false)
    }
  }

  if (!isOwner || !businessId) {
    return (
      <section>
        <h1>Configuración</h1>
        <p className="text-on-surface-variant">Esta sección es solo para propietarios de un negocio.</p>
      </section>
    )
  }

  return (
    <section className="flex flex-col gap-stack-lg">
      <div>
        <h1>Configuración</h1>
        <p className="text-on-surface-variant">Gestiona todos los aspectos de tu negocio.</p>
      </div>

      {loadError && <p role="alert" className="alert" data-testid="business-error">{loadError}</p>}

      {/* Todas las secciones unidas en un único bloque (acordeón compacto, sin huecos). */}
      <div className="overflow-hidden rounded-xl border border-outline-variant bg-surface-container-lowest shadow-card divide-y divide-outline-variant/40">
      {/* Datos */}
      <SectionCard id="datos" title="Datos del negocio" icon="storefront" defaultOpen>
        <div className="flex items-start gap-stack-md" data-testid="business-card">
          <div className="flex-1 min-w-0">
            <h3 className="!mt-0 font-bold text-base" data-testid="business-name">{business?.name ?? '…'}</h3>
            <p className="text-sm text-on-surface-variant break-all">
              ID: <code className="rounded bg-surface-container px-1.5 py-0.5 text-xs" data-testid="business-id">{businessId}</code>
            </p>
            <div className="mt-stack-md">
              <p className="text-xs font-semibold text-on-surface-variant mb-1">Enlace público para tus clientes</p>
              <div className="flex items-center gap-2 flex-wrap">
                <code className="flex-1 min-w-0 break-all rounded bg-surface-container px-2 py-1.5 text-xs" data-testid="business-reserve-link">
                  {`${window.location.origin}/reservar?businessId=${businessId}`}
                </code>
                <button
                  type="button"
                  data-testid="copy-reserve-link"
                  onClick={() => {
                    navigator.clipboard?.writeText(`${window.location.origin}/reservar?businessId=${businessId}`)
                    setLinkCopied(true)
                    setTimeout(() => setLinkCopied(false), 2000)
                  }}
                  className="btn-secondary !py-1.5 text-xs inline-flex items-center gap-1 shrink-0"
                >
                  <span className="material-symbols-outlined text-[16px]">{linkCopied ? 'check' : 'content_copy'}</span>
                  {linkCopied ? 'Copiado' : 'Copiar'}
                </button>
              </div>
              <p className="text-[11px] text-on-surface-variant mt-1">
                Compártelo para que tus clientes reserven online. Tú apuntas reservas desde la <strong>Agenda</strong>.
              </p>
            </div>
          </div>
        </div>
      </SectionCard>

      {/* Perfil público */}
      <SectionCard id="perfil" title="Perfil (Explorar)" icon="badge">
        <p className="text-sm text-on-surface-variant -mt-stack-sm">
          Cómo se ve tu negocio en Explorar: categoría, foto y ubicación (para que aparezca en "negocios cercanos").
        </p>
        <ProfileSection businessId={businessId} business={business} onUpdated={(b) => setBusiness(b)} />
      </SectionCard>

      {/* Servicios */}
      <SectionCard id="servicios" title="Servicios" icon="content_cut">
        {services === null && !loadError && <p className="text-on-surface-variant text-sm">Cargando…</p>}
        {services !== null && services.length === 0 && (
          <p className="text-on-surface-variant text-sm" data-testid="services-empty">Aún no tienes servicios. Crea el primero abajo.</p>
        )}
        {services !== null && services.length > 0 && (
          <ul className="flex flex-col gap-2" data-testid="services-list">
            {services.map((svc) => (
              <ServiceRow key={svc.id} businessId={businessId} service={svc} onChanged={() => loadServices(businessId)} />
            ))}
          </ul>
        )}

        <div className="border-t border-outline-variant/30 pt-stack-md mt-stack-sm">
          {!svcFormOpen ? (
            <button type="button" onClick={() => { setSvcFormError(null); setSvcFormOpen(true) }}
              className="inline-flex items-center gap-1.5 btn-primary self-start" data-testid="new-service-toggle">
              <span className="material-symbols-outlined text-[18px]">add</span>
              Nuevo servicio
            </button>
          ) : (
          <>
          <div className="flex items-center justify-between mb-stack-md">
            <p className="text-sm font-semibold">Nuevo servicio</p>
            <button type="button" onClick={() => setSvcFormOpen(false)}
              className="p-1 rounded-lg text-on-surface-variant hover:bg-surface-container-low transition-colors" aria-label="Cerrar">
              <span className="material-symbols-outlined text-[18px]">close</span>
            </button>
          </div>
          <form onSubmit={handleCreateService} data-testid="create-service-form" className="flex flex-col gap-stack-md">
            {svcFormError && <p role="alert" className="alert" data-testid="create-service-error">{svcFormError}</p>}
            <div className="field">
              <label className="field-label" htmlFor="service-name">Nombre</label>
              <input id="service-name" type="text" className="field-input" data-testid="service-name"
                value={svcName} onChange={(e) => setSvcName(e.target.value)} placeholder="Corte de cabello" required />
            </div>
            <div className="grid grid-cols-2 gap-stack-md">
              <div className="field">
                <label className="field-label" htmlFor="service-duration">Duración (min)</label>
                <input id="service-duration" type="number" className="field-input" data-testid="service-duration"
                  value={svcDuration} onChange={(e) => setSvcDuration(e.target.value)} min={5} step={5} required />
              </div>
              <div className="field">
                <label className="field-label" htmlFor="service-price">Precio (€) — vacío = gratis</label>
                <input id="service-price" type="number" className="field-input" data-testid="service-price"
                  value={svcPrice} onChange={(e) => setSvcPrice(e.target.value)} min={0} step="0.01" placeholder="25" />
              </div>
            </div>
            <div className="field">
              <label className="field-label" htmlFor="service-description">Descripción (opcional)</label>
              <input id="service-description" type="text" className="field-input" data-testid="service-description"
                value={svcDescription} onChange={(e) => setSvcDescription(e.target.value)} placeholder="Corte clásico" />
            </div>
            <div className="field">
              <label className="field-label" htmlFor="service-color">Color</label>
              <input id="service-color" type="color" className="h-11 w-16 rounded-lg border border-outline-variant bg-surface-container-lowest p-1" data-testid="service-color"
                value={svcColor} onChange={(e) => setSvcColor(e.target.value)} />
            </div>
            <button type="submit" className="btn-primary self-start" data-testid="create-service-submit" disabled={svcSaving}>
              {svcSaving ? 'Creando…' : 'Crear servicio'}
            </button>
          </form>
          </>
          )}
        </div>
      </SectionCard>

      {/* Equipo */}
      <SectionCard id="equipo" title="Equipo" icon="group">
        <p className="text-sm text-on-surface-variant -mt-stack-sm">
          Los trabajadores de tu negocio. Los clientes eligen con quién reservar.
        </p>
        <TeamSection businessId={businessId} services={services} />
      </SectionCard>

      {/* Horario */}
      <SectionCard id="horario" title="Horario semanal" icon="schedule">
        <p className="text-sm text-on-surface-variant -mt-stack-sm">
          Define cuándo abres cada día. Los clientes solo verán huecos dentro de este horario.
        </p>
        <HoursSection businessId={businessId} />
      </SectionCard>

      {/* Festivos */}
      <SectionCard id="festivos" title="Festivos y días cerrados" icon="beach_access">
        <p className="text-sm text-on-surface-variant -mt-stack-sm">
          Los días añadidos aquí no tendrán disponibilidad para reservas.
        </p>
        <HolidaysSection businessId={businessId} />
      </SectionCard>

      {/* Modo de reservas */}
      <SectionCard id="modo-reservas" title="Modo de reservas" icon="event_available">
        <p className="text-sm text-on-surface-variant -mt-stack-sm">
          En <strong>reservas online</strong> tus clientes reservan por internet y tu negocio aparece en Explorar.
          En <strong>solo calendario</strong> usas Slotify como agenda: solo tú apuntas las reservas y el negocio no aparece en Explorar.
        </p>
        {bookingModeError && <p role="alert" className="alert text-sm" data-testid="booking-mode-error">{bookingModeError}</p>}
        <div className="inline-flex rounded-full border border-outline-variant/50 bg-surface-container p-1 gap-1 self-start">
          <button
            type="button"
            disabled={bookingModeSaving}
            onClick={() => handleSetBookingMode('online')}
            data-testid="booking-mode-online"
            className={`rounded-full px-5 py-2 text-sm font-bold transition-all ${
              business?.bookingMode !== 'calendar_only'
                ? 'bg-primary text-on-primary shadow-sm'
                : 'text-on-surface-variant hover:text-on-surface'
            }`}
          >
            Reservas online
          </button>
          <button
            type="button"
            disabled={bookingModeSaving}
            onClick={() => handleSetBookingMode('calendar_only')}
            data-testid="booking-mode-calendar-only"
            className={`rounded-full px-5 py-2 text-sm font-bold transition-all ${
              business?.bookingMode === 'calendar_only'
                ? 'bg-primary text-on-primary shadow-sm'
                : 'text-on-surface-variant hover:text-on-surface'
            }`}
          >
            Solo calendario
          </button>
        </div>
        {business?.bookingMode === 'calendar_only' && (
          <p className="inline-flex items-center gap-1 text-xs font-semibold text-on-surface-variant" data-testid="booking-mode-calendar-only-active">
            <span className="material-symbols-outlined text-[16px]">info</span>
            Tus clientes no pueden reservar online; apunta las reservas desde la Agenda.
          </p>
        )}
      </SectionCard>

      {/* Confirmación */}
      <SectionCard id="confirmacion" title="Confirmación de reservas" icon="verified">
        <p className="text-sm text-on-surface-variant -mt-stack-sm">
          En modo <strong>automático</strong> las reservas se confirman al instante. En modo <strong>manual</strong> quedan pendientes hasta que las confirmes desde la Agenda.
        </p>
        {confError && <p role="alert" className="alert text-sm">{confError}</p>}
        <div className="flex items-center gap-stack-md flex-wrap">
          <div className="inline-flex rounded-full border border-outline-variant/50 bg-surface-container p-1 gap-1">
            <button
              type="button"
              disabled={confSaving}
              onClick={() => handleSetMode('auto')}
              data-testid="confirmation-mode-auto"
              className={`rounded-full px-5 py-2 text-sm font-bold transition-all ${
                business?.confirmationMode !== 'manual'
                  ? 'bg-primary text-on-primary shadow-sm'
                  : 'text-on-surface-variant hover:text-on-surface'
              }`}
            >
              Automático
            </button>
            <button
              type="button"
              disabled={confSaving}
              onClick={() => handleSetMode('manual')}
              data-testid="confirmation-mode-manual"
              className={`rounded-full px-5 py-2 text-sm font-bold transition-all ${
                business?.confirmationMode === 'manual'
                  ? 'bg-primary text-on-primary shadow-sm'
                  : 'text-on-surface-variant hover:text-on-surface'
              }`}
            >
              Manual
            </button>
          </div>
          {confSaving && <span className="text-sm text-on-surface-variant">Guardando…</span>}
          {confSaved && !confSaving && (
            <span className="inline-flex items-center gap-1 text-sm font-semibold text-secondary">
              <span className="material-symbols-outlined text-[16px]">check_circle</span> Guardado
            </span>
          )}
        </div>
      </SectionCard>

      {/* Ventana de cancelación */}
      <SectionCard id="cancelacion" title="Ventana de cancelación" icon="timer">
        <p className="text-sm text-on-surface-variant -mt-stack-sm">
          Tiempo mínimo de antelación para que los clientes puedan cancelar o reprogramar. <strong>0 horas</strong> = sin restricción.
        </p>
        {cutoffError && <p role="alert" className="alert text-sm">{cutoffError}</p>}
        <form onSubmit={handleSaveCutoff} className="flex flex-wrap items-end gap-stack-md">
          <div className="field !gap-1">
            <label className="field-label text-xs" htmlFor="cutoff-hours">Horas de antelación mínima</label>
            <div className="flex items-center gap-2">
              <input
                id="cutoff-hours"
                type="number"
                className="field-input w-28"
                data-testid="cutoff-hours-input"
                value={cutoffHours}
                onChange={(e) => { setCutoffHours(e.target.value); setCutoffSaved(false) }}
                min={0}
                max={720}
                step={1}
                required
              />
              <span className="text-sm text-on-surface-variant">horas</span>
            </div>
          </div>
          <button type="submit" className="btn-primary" data-testid="cutoff-save" disabled={cutoffSaving}>
            {cutoffSaving ? 'Guardando…' : 'Guardar'}
          </button>
          {cutoffSaved && (
            <span className="inline-flex items-center gap-1 text-sm font-semibold text-secondary self-center">
              <span className="material-symbols-outlined text-[16px]">check_circle</span> Guardado
            </span>
          )}
        </form>
      </SectionCard>

      {/* Avisos a clientes */}
      <SectionCard id="notificaciones" title="Avisos a clientes" icon="notifications">
        <NotificationsSection businessId={businessId} business={business} onUpdated={(b) => setBusiness(b)} />
      </SectionCard>

      {/* Plan */}
      <SectionCard id="plan" title="Plan" icon="workspace_premium">
        <div className="flex flex-col gap-stack-md" data-testid="plan-section">
          <p className="text-sm text-on-surface-variant -mt-stack-sm">
            El plan Premium desbloquea trabajadores ilimitados y más.
          </p>
          {planError && <p role="alert" className="alert text-sm" data-testid="plan-error">{planError}</p>}
          <div className="flex items-center gap-stack-md flex-wrap">
            <span className="text-sm font-semibold">Plan actual:</span>
            <span
              data-testid="plan-current"
              className={`inline-flex items-center gap-1 rounded-full px-3 py-1 text-sm font-bold ${
                business?.plan === 'premium'
                  ? 'bg-primary-container/30 text-primary'
                  : 'bg-surface-container text-on-surface-variant'
              }`}
            >
              <span className="material-symbols-outlined text-[16px]">workspace_premium</span>
              {business?.plan === 'premium' ? 'Premium' : 'Free'}
            </span>
          </div>

          {business?.plan === 'premium' ? (
            <div className="flex items-center gap-stack-md flex-wrap">
              <span className="inline-flex items-center gap-1 text-sm font-semibold text-secondary">
                <span className="material-symbols-outlined text-[18px]">check_circle</span>
                Plan Premium activo
              </span>
              <button
                type="button"
                className="btn-secondary"
                data-testid="plan-downgrade"
                disabled={planSaving}
                onClick={() => handleSetPlan('free')}
              >
                {planSaving ? 'Cambiando…' : 'Volver a Free'}
              </button>
            </div>
          ) : (
            <button
              type="button"
              className="btn-primary self-start"
              data-testid="plan-upgrade"
              disabled={planSaving}
              onClick={() => handleSetPlan('premium')}
            >
              {planSaving ? 'Cambiando…' : 'Mejorar a Premium'}
            </button>
          )}

          {business?.plan !== 'premium' && ((services?.length ?? 0) > 5 || (staffCount ?? 0) > 1) && (
            <div className="rounded-xl border border-outline-variant bg-surface-container px-stack-md py-3 text-sm" data-testid="plan-over-limit">
              <p className="font-semibold text-on-surface">Estás por encima de los límites del plan Free</p>
              <p className="text-on-surface-variant text-xs mt-1">
                El plan Free incluye 1 trabajador y 5 servicios. Lo que ya tienes seguirá funcionando con normalidad,
                pero no podrás añadir más hasta que te ajustes o vuelvas a Premium.
              </p>
            </div>
          )}

          <p className="text-xs text-on-surface-variant">
            En esta versión el cambio de plan es inmediato y sin pago (demo).
          </p>
        </div>
      </SectionCard>
      </div>
    </section>
  )
}
