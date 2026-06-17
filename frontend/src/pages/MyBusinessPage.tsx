import { useCallback, useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'
import { businessService } from '../services/businessService'
import { getApiError } from '../services/apiClient'
import type { BusinessResponse, ServiceResponse } from '../types/api'

function formatPrice(price: number | null): string {
  if (price === null) return 'Gratis'
  return new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR' }).format(price)
}

export function MyBusinessPage() {
  const { businessId, isOwner } = useAuth()

  const [business, setBusiness] = useState<BusinessResponse | null>(null)
  const [services, setServices] = useState<ServiceResponse[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  const [name, setName] = useState('')
  const [durationMinutes, setDurationMinutes] = useState('30')
  const [price, setPrice] = useState('')
  const [description, setDescription] = useState('')
  const [color, setColor] = useState('#7C3AED')
  const [saving, setSaving] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)

  const loadServices = useCallback(async (id: string) => {
    try {
      setServices(await businessService.listServices(id))
    } catch (err) {
      setError(getApiError(err)?.message ?? 'No se pudieron cargar los servicios.')
    }
  }, [])

  useEffect(() => {
    if (!businessId) return
    let active = true
    businessService
      .listMine()
      .then((list) => {
        if (active) setBusiness(list.find((b) => b.id === businessId) ?? list[0] ?? null)
      })
      .catch((err) => {
        if (active) setError(getApiError(err)?.message ?? 'No se pudo cargar tu negocio.')
      })
    loadServices(businessId)
    return () => {
      active = false
    }
  }, [businessId, loadServices])

  async function handleCreateService(e: FormEvent) {
    e.preventDefault()
    if (!businessId) return
    setFormError(null)
    setSaving(true)
    try {
      await businessService.createService(businessId, {
        name: name.trim(),
        description: description.trim() || null,
        durationMinutes: Number(durationMinutes),
        price: price.trim() === '' ? null : Number(price),
        color: color || null,
      })
      setName('')
      setDurationMinutes('30')
      setPrice('')
      setDescription('')
      setColor('#7C3AED')
      await loadServices(businessId)
    } catch (err) {
      const apiErr = getApiError(err)
      setFormError(
        apiErr?.error === 'limit_reached'
          ? 'Has alcanzado el límite de servicios de tu plan.'
          : apiErr?.message ?? 'No se pudo crear el servicio.',
      )
    } finally {
      setSaving(false)
    }
  }

  if (!isOwner || !businessId) {
    return (
      <section>
        <h1>Mi negocio</h1>
        <p className="text-on-surface-variant">Esta sección es solo para propietarios de un negocio.</p>
      </section>
    )
  }

  return (
    <section className="flex flex-col gap-stack-lg">
      <div>
        <h1>Mi negocio</h1>
        <p className="text-on-surface-variant">Gestiona tu negocio y sus servicios.</p>
      </div>

      {error && (
        <p role="alert" className="alert" data-testid="business-error">
          {error}
        </p>
      )}

      {/* Tarjeta del negocio */}
      <div className="card flex items-start gap-stack-md" data-testid="business-card">
        <span className="w-12 h-12 rounded-xl bg-primary-container text-on-primary flex items-center justify-center shrink-0">
          <span className="material-symbols-outlined">storefront</span>
        </span>
        <div className="flex-1 min-w-0">
          <h2 className="!mt-0" data-testid="business-name">{business?.name ?? '…'}</h2>
          <p className="text-sm text-on-surface-variant break-all">
            ID: <code className="rounded bg-surface-container px-1.5 py-0.5 text-xs" data-testid="business-id">{businessId}</code>
          </p>
          <Link
            to={`/reservar?businessId=${businessId}`}
            data-testid="business-reserve-link"
            className="mt-stack-sm inline-flex items-center gap-1 text-sm font-semibold text-primary hover:underline"
          >
            Reservar en este negocio
            <span className="material-symbols-outlined text-[18px]">arrow_forward</span>
          </Link>
        </div>
      </div>

      {/* Servicios */}
      <div>
        <h2 className="mb-stack-sm">Servicios</h2>
        {services === null && !error && <p className="text-on-surface-variant">Cargando…</p>}
        {services !== null && services.length === 0 && (
          <p className="text-on-surface-variant" data-testid="services-empty">
            Aún no tienes servicios. Crea el primero abajo.
          </p>
        )}
        {services !== null && services.length > 0 && (
          <ul className="flex flex-col gap-stack-sm" data-testid="services-list">
            {services.map((svc) => (
              <li key={svc.id} className="glass-card rounded-xl p-stack-md flex items-center gap-stack-md" data-testid="service-item">
                <span className="w-3.5 h-3.5 rounded-full shrink-0 ring-1 ring-black/10" style={{ background: svc.color ?? '#cbd5e1' }} aria-hidden />
                <div className="flex-1 min-w-0">
                  <strong className="font-semibold">{svc.name}</strong>
                  <p className="text-sm text-on-surface-variant">
                    {svc.durationMinutes} min · {formatPrice(svc.price)}
                    {svc.description ? ` · ${svc.description}` : ''}
                  </p>
                </div>
              </li>
            ))}
          </ul>
        )}
      </div>

      {/* Alta de servicio */}
      <div className="card">
        <h2 className="!mt-0 mb-stack-md">Nuevo servicio</h2>
        <form onSubmit={handleCreateService} data-testid="create-service-form" className="flex flex-col gap-stack-md">
          {formError && (
            <p role="alert" className="alert" data-testid="create-service-error">
              {formError}
            </p>
          )}
          <div className="field">
            <label className="field-label" htmlFor="service-name">Nombre</label>
            <input id="service-name" type="text" className="field-input" data-testid="service-name"
              value={name} onChange={(e) => setName(e.target.value)} placeholder="Corte de cabello" required />
          </div>
          <div className="grid grid-cols-2 gap-stack-md">
            <div className="field">
              <label className="field-label" htmlFor="service-duration">Duración (min)</label>
              <input id="service-duration" type="number" className="field-input" data-testid="service-duration"
                value={durationMinutes} onChange={(e) => setDurationMinutes(e.target.value)} min={5} step={5} required />
            </div>
            <div className="field">
              <label className="field-label" htmlFor="service-price">Precio (€) — vacío = gratis</label>
              <input id="service-price" type="number" className="field-input" data-testid="service-price"
                value={price} onChange={(e) => setPrice(e.target.value)} min={0} step="0.01" placeholder="25" />
            </div>
          </div>
          <div className="field">
            <label className="field-label" htmlFor="service-description">Descripción (opcional)</label>
            <input id="service-description" type="text" className="field-input" data-testid="service-description"
              value={description} onChange={(e) => setDescription(e.target.value)} placeholder="Corte clásico" />
          </div>
          <div className="field">
            <label className="field-label" htmlFor="service-color">Color</label>
            <input id="service-color" type="color" className="h-11 w-16 rounded-lg border border-outline-variant bg-surface-container-lowest p-1" data-testid="service-color"
              value={color} onChange={(e) => setColor(e.target.value)} />
          </div>
          <button type="submit" className="btn-primary self-start" data-testid="create-service-submit" disabled={saving}>
            {saving ? 'Creando…' : 'Crear servicio'}
          </button>
        </form>
      </div>
    </section>
  )
}
