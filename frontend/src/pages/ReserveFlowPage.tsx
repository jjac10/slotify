import { useState } from 'react'
import type { FormEvent } from 'react'
import { businessService } from '../services/businessService'
import { getApiError } from '../services/apiClient'
import type { ServiceResponse } from '../types/api'

/**
 * Shell del flujo de reserva. Carga los servicios públicos de un negocio dado
 * su id. El paso final (crear la reserva) necesita además un `staffId`, pero la
 * API aún no expone descubrimiento de negocio/trabajador (huecos aplazados en el
 * backend), así que de momento esto deja el flujo listo para conectarse en cuanto
 * existan esos endpoints.
 */
export function ReserveFlowPage() {
  const [businessId, setBusinessId] = useState('')
  const [services, setServices] = useState<ServiceResponse[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setServices(null)
    setLoading(true)
    try {
      setServices(await businessService.listServices(businessId.trim()))
    } catch (err) {
      setError(getApiError(err)?.message ?? 'No se pudieron cargar los servicios.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <section>
      <h1>Reservar</h1>
      <p>Introduce el identificador de un negocio para ver sus servicios.</p>

      <form onSubmit={handleSubmit}>
        <label>
          Business ID
          <input
            type="text"
            data-testid="reserve-business-id"
            value={businessId}
            onChange={(e) => setBusinessId(e.target.value)}
            placeholder="uuid del negocio"
            required
          />
        </label>
        <button type="submit" data-testid="reserve-load-services" disabled={loading}>
          {loading ? 'Cargando…' : 'Ver servicios'}
        </button>
      </form>

      {error && (
        <p role="alert" data-testid="reserve-error">
          {error}
        </p>
      )}

      {services !== null && services.length === 0 && (
        <p data-testid="reserve-no-services">Este negocio no tiene servicios publicados.</p>
      )}

      {services !== null && services.length > 0 && (
        <ul data-testid="reserve-services">
          {services.map((service) => (
            <li key={service.id}>
              <strong>{service.name}</strong> · {service.durationMinutes} min
              {service.price !== null ? ` · ${service.price} €` : ''}
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
