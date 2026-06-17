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

  // Formulario de alta de servicio
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
      // Reset + recarga la lista
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
        <p>Esta sección es solo para propietarios de un negocio.</p>
      </section>
    )
  }

  return (
    <section>
      <h1>Mi negocio</h1>

      {error && (
        <p role="alert" data-testid="business-error">
          {error}
        </p>
      )}

      {/* Datos del negocio */}
      <div className="card" data-testid="business-card">
        <h2 data-testid="business-name">{business?.name ?? '…'}</h2>
        <p className="muted">
          ID del negocio: <code data-testid="business-id">{businessId}</code>
        </p>
        <Link to={`/reservar?businessId=${businessId}`} data-testid="business-reserve-link">
          Reservar en este negocio →
        </Link>
      </div>

      {/* Servicios */}
      <h2>Servicios</h2>
      {services === null && !error && <p>Cargando…</p>}
      {services !== null && services.length === 0 && (
        <p data-testid="services-empty">Aún no tienes servicios. Crea el primero abajo.</p>
      )}
      {services !== null && services.length > 0 && (
        <ul className="service-list" data-testid="services-list">
          {services.map((svc) => (
            <li key={svc.id} className="card" data-testid="service-item">
              <span className="service-color" style={{ background: svc.color ?? '#cbd5e1' }} aria-hidden />
              <div>
                <strong>{svc.name}</strong>
                <p className="muted">
                  {svc.durationMinutes} min · {formatPrice(svc.price)}
                  {svc.description ? ` · ${svc.description}` : ''}
                </p>
              </div>
            </li>
          ))}
        </ul>
      )}

      {/* Alta de servicio */}
      <h2>Nuevo servicio</h2>
      <form onSubmit={handleCreateService} data-testid="create-service-form">
        {formError && (
          <p role="alert" data-testid="create-service-error">
            {formError}
          </p>
        )}
        <label>
          Nombre
          <input
            type="text"
            data-testid="service-name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="Corte de cabello"
            required
          />
        </label>
        <label>
          Duración (minutos)
          <input
            type="number"
            data-testid="service-duration"
            value={durationMinutes}
            onChange={(e) => setDurationMinutes(e.target.value)}
            min={5}
            step={5}
            required
          />
        </label>
        <label>
          Precio (€) — vacío = gratis
          <input
            type="number"
            data-testid="service-price"
            value={price}
            onChange={(e) => setPrice(e.target.value)}
            min={0}
            step="0.01"
            placeholder="25"
          />
        </label>
        <label>
          Descripción (opcional)
          <input
            type="text"
            data-testid="service-description"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            placeholder="Corte clásico"
          />
        </label>
        <label>
          Color
          <input
            type="color"
            data-testid="service-color"
            value={color}
            onChange={(e) => setColor(e.target.value)}
          />
        </label>
        <button type="submit" data-testid="create-service-submit" disabled={saving}>
          {saving ? 'Creando…' : 'Crear servicio'}
        </button>
      </form>
    </section>
  )
}
