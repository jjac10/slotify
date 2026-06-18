import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { businessService } from '../services/businessService'
import { getApiError } from '../services/apiClient'
import type { BusinessResponse } from '../types/api'

export function ExplorePage() {
  const [query, setQuery] = useState('')
  const [businesses, setBusinesses] = useState<BusinessResponse[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    const handle = setTimeout(() => {
      businessService
        .searchPublic(query.trim() || undefined)
        .then((data) => {
          if (active) {
            setBusinesses(data)
            setError(null)
          }
        })
        .catch((err) => {
          if (active) setError(getApiError(err)?.message ?? 'No se pudieron cargar los negocios.')
        })
    }, 250)
    return () => {
      active = false
      clearTimeout(handle)
    }
  }, [query])

  return (
    <section>
      <h1>Explorar negocios</h1>
      <p className="text-on-surface-variant mb-stack-md">Encuentra dónde reservar.</p>

      {/* Buscador por nombre */}
      <div className="relative mb-stack-md">
        <span className="material-symbols-outlined pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-on-surface-variant">
          search
        </span>
        <input
          type="search"
          data-testid="explore-search"
          className="field-input w-full !pl-11"
          placeholder="Busca por nombre (barbería, spa…)"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          aria-label="Buscar negocio por nombre"
        />
      </div>

      {error && (
        <p role="alert" className="alert" data-testid="explore-error">
          {error}
        </p>
      )}

      {businesses === null && !error && <p className="text-on-surface-variant">Cargando…</p>}

      {businesses !== null && businesses.length === 0 && (
        <div className="card flex flex-col items-center text-center py-stack-xl" data-testid="explore-empty">
          <span className="material-symbols-outlined text-[40px] text-on-surface-variant/40">storefront</span>
          <p className="mt-stack-sm font-semibold">No hay negocios que coincidan.</p>
          <p className="text-sm text-on-surface-variant">Prueba con otro nombre.</p>
        </div>
      )}

      {businesses !== null && businesses.length > 0 && (
        <ul className="flex flex-col gap-stack-sm" data-testid="explore-list">
          {businesses.map((b) => (
            <li key={b.id} className="card flex items-center gap-stack-md" data-testid="explore-item">
              <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-primary-container text-on-primary">
                <span className="material-symbols-outlined">storefront</span>
              </span>
              <div className="min-w-0 flex-1">
                <p className="truncate font-bold">{b.name}</p>
                <p className="text-sm text-on-surface-variant">Disponible para reservar</p>
              </div>
              <Link to={`/reservar?businessId=${b.id}`} className="btn-primary py-2 text-sm" data-testid="explore-reserve">
                Reservar
              </Link>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
