import { useCallback, useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'
import { businessService } from '../services/businessService'
import { getApiError } from '../services/apiClient'
import type { BusinessHoliday, BusinessHour, BusinessResponse, ServiceResponse } from '../types/api'

// ─── helpers ────────────────────────────────────────────────────────────────

function formatPrice(price: number | null): string {
  if (price === null) return 'Gratis'
  return new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR' }).format(price)
}

function SectionCard({ title, icon, children }: { title: string; icon: string; children: React.ReactNode }) {
  return (
    <div className="card flex flex-col gap-stack-md">
      <div className="flex items-center gap-stack-sm border-b border-outline-variant/30 pb-stack-md -mt-stack-sm">
        <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-primary-container/15 text-primary">
          <span className="material-symbols-outlined text-[20px]">{icon}</span>
        </span>
        <h2 className="!mt-0 text-base font-bold">{title}</h2>
      </div>
      {children}
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

function HolidaysSection({ businessId }: { businessId: string }) {
  const [holidays, setHolidays] = useState<BusinessHoliday[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [date, setDate] = useState('')
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
    setAddError(null)
    setAdding(true)
    try {
      await businessService.addHoliday(businessId, date, reason.trim() || undefined)
      setDate('')
      setReason('')
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
        <ul className="flex flex-col gap-2">
          {holidays
            .slice()
            .sort((a, b) => a.holidayDate.localeCompare(b.holidayDate))
            .map((h) => (
              <li key={h.id} className="glass-card rounded-xl px-stack-md py-3 flex items-center gap-stack-md">
                <span className="material-symbols-outlined text-[20px] text-on-surface-variant">beach_access</span>
                <div className="flex-1 min-w-0">
                  <p className="font-semibold text-sm">{h.holidayDate}</p>
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

      <form onSubmit={handleAdd} className="flex flex-col gap-stack-sm">
        <p className="text-sm font-semibold">Añadir día festivo</p>
        {addError && <p role="alert" className="alert text-xs">{addError}</p>}
        <div className="flex flex-wrap gap-2 items-end">
          <div className="field !gap-1">
            <label className="field-label text-xs" htmlFor="holiday-date">Fecha</label>
            <input id="holiday-date" type="date" className="field-input !py-2 w-44" min={today}
              value={date} onChange={(e) => setDate(e.target.value)} required />
          </div>
          <div className="field !gap-1 flex-1 min-w-40">
            <label className="field-label text-xs" htmlFor="holiday-reason">Motivo (opcional)</label>
            <input id="holiday-reason" type="text" className="field-input !py-2"
              value={reason} onChange={(e) => setReason(e.target.value)} placeholder="Navidad, vacaciones…" />
          </div>
          <button type="submit" className="btn-primary !py-2 text-sm" disabled={adding}>
            {adding ? 'Añadiendo…' : 'Añadir'}
          </button>
        </div>
      </form>
    </div>
  )
}

// ─── Página principal ────────────────────────────────────────────────────────

export function BusinessSettingsPage() {
  const { businessId, isOwner } = useAuth()

  const [business, setBusiness] = useState<BusinessResponse | null>(null)
  const [services, setServices] = useState<ServiceResponse[] | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)

  // Servicios — form
  const [svcName, setSvcName] = useState('')
  const [svcDuration, setSvcDuration] = useState('30')
  const [svcPrice, setSvcPrice] = useState('')
  const [svcDescription, setSvcDescription] = useState('')
  const [svcColor, setSvcColor] = useState('#7C3AED')
  const [svcSaving, setSvcSaving] = useState(false)
  const [svcFormError, setSvcFormError] = useState<string | null>(null)

  // Confirmación
  const [confSaving, setConfSaving] = useState(false)
  const [confSaved, setConfSaved] = useState(false)
  const [confError, setConfError] = useState<string | null>(null)

  // Ventana de cancelación
  const [cutoffHours, setCutoffHours] = useState('')
  const [cutoffSaving, setCutoffSaving] = useState(false)
  const [cutoffSaved, setCutoffSaved] = useState(false)
  const [cutoffError, setCutoffError] = useState<string | null>(null)

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
          setCutoffHours(String(b.cancellationCutoffHours))
        }
      })
      .catch((err) => { if (active) setLoadError(getApiError(err)?.message ?? 'No se pudo cargar tu negocio.') })
    loadServices(businessId)
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

      {/* Datos */}
      <SectionCard title="Datos del negocio" icon="storefront">
        <div className="flex items-start gap-stack-md" data-testid="business-card">
          <div className="flex-1 min-w-0">
            <h3 className="!mt-0 font-bold text-base" data-testid="business-name">{business?.name ?? '…'}</h3>
            <p className="text-sm text-on-surface-variant break-all">
              ID: <code className="rounded bg-surface-container px-1.5 py-0.5 text-xs" data-testid="business-id">{businessId}</code>
            </p>
            <Link
              to={`/reservar?businessId=${businessId}`}
              data-testid="business-reserve-link"
              className="mt-stack-sm inline-flex items-center gap-1 text-sm font-semibold text-primary hover:underline"
            >
              Enlace de reserva
              <span className="material-symbols-outlined text-[18px]">arrow_forward</span>
            </Link>
          </div>
        </div>
      </SectionCard>

      {/* Servicios */}
      <SectionCard title="Servicios" icon="content_cut">
        {services === null && !loadError && <p className="text-on-surface-variant text-sm">Cargando…</p>}
        {services !== null && services.length === 0 && (
          <p className="text-on-surface-variant text-sm" data-testid="services-empty">Aún no tienes servicios. Crea el primero abajo.</p>
        )}
        {services !== null && services.length > 0 && (
          <ul className="flex flex-col gap-2" data-testid="services-list">
            {services.map((svc) => (
              <li key={svc.id} className="glass-card rounded-xl p-stack-md flex items-center gap-stack-md" data-testid="service-item">
                <span className="w-3.5 h-3.5 rounded-full shrink-0 ring-1 ring-black/10" style={{ background: svc.color ?? '#cbd5e1' }} aria-hidden />
                <div className="flex-1 min-w-0">
                  <strong className="font-semibold text-sm">{svc.name}</strong>
                  <p className="text-xs text-on-surface-variant">
                    {svc.durationMinutes} min · {formatPrice(svc.price)}
                    {svc.description ? ` · ${svc.description}` : ''}
                  </p>
                </div>
              </li>
            ))}
          </ul>
        )}

        <div className="border-t border-outline-variant/30 pt-stack-md mt-stack-sm">
          <p className="text-sm font-semibold mb-stack-md">Nuevo servicio</p>
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
        </div>
      </SectionCard>

      {/* Horario */}
      <SectionCard title="Horario semanal" icon="schedule">
        <p className="text-sm text-on-surface-variant -mt-stack-sm">
          Define cuándo abres cada día. Los clientes solo verán huecos dentro de este horario.
        </p>
        <HoursSection businessId={businessId} />
      </SectionCard>

      {/* Festivos */}
      <SectionCard title="Festivos y días cerrados" icon="beach_access">
        <p className="text-sm text-on-surface-variant -mt-stack-sm">
          Los días añadidos aquí no tendrán disponibilidad para reservas.
        </p>
        <HolidaysSection businessId={businessId} />
      </SectionCard>

      {/* Confirmación */}
      <SectionCard title="Confirmación de reservas" icon="verified">
        <p className="text-sm text-on-surface-variant -mt-stack-sm">
          En modo <strong>automático</strong> las reservas se confirman al instante. En modo <strong>manual</strong> quedan pendientes hasta que las confirmes desde la Agenda.
        </p>
        {confError && <p role="alert" className="alert text-sm">{confError}</p>}
        <div className="flex flex-wrap gap-2">
          {(['auto', 'manual'] as const).map((m) => (
            <button
              key={m}
              type="button"
              disabled={confSaving || business?.confirmationMode === m}
              onClick={() => handleSetMode(m)}
              data-testid={`confirmation-mode-${m}`}
              className={`rounded-xl px-5 py-2.5 text-sm font-bold transition-colors disabled:opacity-60 ${
                business?.confirmationMode === m
                  ? 'bg-primary-container text-on-primary shadow-card'
                  : 'bg-surface-container text-on-surface-variant hover:bg-surface-container-high'
              }`}
            >
              {m === 'auto' ? 'Automático' : 'Manual'}
            </button>
          ))}
          {confSaved && (
            <span className="inline-flex items-center gap-1 text-sm font-semibold text-secondary self-center">
              <span className="material-symbols-outlined text-[16px]">check_circle</span> Guardado
            </span>
          )}
        </div>
      </SectionCard>

      {/* Ventana de cancelación */}
      <SectionCard title="Ventana de cancelación" icon="timer">
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
    </section>
  )
}
