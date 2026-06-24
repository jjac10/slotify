import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { businessService } from '../services/businessService'
import { getApiError } from '../services/apiClient'
import { BUSINESS_CATEGORIES, categoryIcon, categoryLabel } from '../constants/categories'
import { RatingStars } from '../components/Stars'
import type { BusinessResponse } from '../types/api'

interface Coords { lat: number; lng: number }

/** Distancia en km entre dos puntos (haversine). */
function distanceKm(a: Coords, b: Coords): number {
  const R = 6371
  const dLat = ((b.lat - a.lat) * Math.PI) / 180
  const dLng = ((b.lng - a.lng) * Math.PI) / 180
  const lat1 = (a.lat * Math.PI) / 180
  const lat2 = (b.lat * Math.PI) / 180
  const h = Math.sin(dLat / 2) ** 2 + Math.cos(lat1) * Math.cos(lat2) * Math.sin(dLng / 2) ** 2
  return 2 * R * Math.asin(Math.sqrt(h))
}

function formatDistance(km: number): string {
  return km < 1 ? `${Math.round(km * 1000)} m` : `${km.toFixed(1)} km`
}

export function ExplorePage() {
  const [query, setQuery] = useState('')
  const [category, setCategory] = useState<string | null>(null)
  const [businesses, setBusinesses] = useState<BusinessResponse[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [coords, setCoords] = useState<Coords | null>(null)
  const [locating, setLocating] = useState(false)
  const [locError, setLocError] = useState<string | null>(null)
  const [selected, setSelected] = useState<BusinessResponse | null>(null)

  useEffect(() => {
    let active = true
    const handle = setTimeout(() => {
      businessService
        .searchPublic(query.trim() || undefined, category ?? undefined)
        .then((data) => { if (active) { setBusinesses(data); setError(null) } })
        .catch((err) => { if (active) setError(getApiError(err)?.message ?? 'No se pudieron cargar los negocios.') })
    }, 250)
    return () => { active = false; clearTimeout(handle) }
  }, [query, category])

  function locate() {
    if (!navigator.geolocation) { setLocError('Tu navegador no permite geolocalización.'); return }
    setLocating(true)
    setLocError(null)
    navigator.geolocation.getCurrentPosition(
      (pos) => { setCoords({ lat: pos.coords.latitude, lng: pos.coords.longitude }); setLocating(false) },
      () => { setLocError('No se pudo obtener tu ubicación.'); setLocating(false) },
      { enableHighAccuracy: false, timeout: 8000 },
    )
  }

  // Distancia por negocio + orden por cercanía cuando hay ubicación.
  const items = useMemo(() => {
    if (!businesses) return null
    const withDist = businesses.map((b) => ({
      b,
      dist: coords && b.latitude != null && b.longitude != null
        ? distanceKm(coords, { lat: b.latitude, lng: b.longitude })
        : null,
    }))
    if (coords) {
      withDist.sort((x, y) => (x.dist ?? Infinity) - (y.dist ?? Infinity))
    }
    return withDist
  }, [businesses, coords])

  return (
    <section>
      <h1>Explorar negocios</h1>
      <p className="text-on-surface-variant mb-stack-md">Encuentra dónde reservar.</p>

      {/* Buscador por nombre */}
      <div className="relative mb-stack-sm">
        <span className="material-symbols-outlined pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-on-surface-variant">search</span>
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

      {/* Filtro por categoría */}
      <div className="mb-stack-sm flex gap-2 overflow-x-auto hide-scrollbar pb-1" data-testid="category-filter">
        <button type="button" onClick={() => setCategory(null)}
          className={`shrink-0 rounded-full px-3 py-1.5 text-sm font-semibold transition-colors ${
            category === null ? 'bg-primary text-on-primary' : 'bg-surface-container text-on-surface-variant hover:bg-surface-container-high'
          }`}>
          Todas
        </button>
        {BUSINESS_CATEGORIES.map((c) => (
          <button key={c.code} type="button" onClick={() => setCategory(c.code)} data-testid="category-chip" data-category={c.code}
            className={`shrink-0 inline-flex items-center gap-1 rounded-full px-3 py-1.5 text-sm font-semibold transition-colors ${
              category === c.code ? 'bg-primary text-on-primary' : 'bg-surface-container text-on-surface-variant hover:bg-surface-container-high'
            }`}>
            <span className="material-symbols-outlined text-[16px]">{c.icon}</span>
            {c.label}
          </button>
        ))}
      </div>

      {/* Cerca de mí */}
      <div className="mb-stack-md flex items-center gap-stack-md flex-wrap">
        <button type="button" onClick={locate} disabled={locating} data-testid="explore-locate"
          className="inline-flex items-center gap-1 rounded-full border border-outline-variant px-3 py-1.5 text-sm font-semibold text-on-surface-variant hover:bg-surface-container-low disabled:opacity-60">
          <span className="material-symbols-outlined text-[18px]">{coords ? 'my_location' : 'location_searching'}</span>
          {locating ? 'Localizando…' : coords ? 'Ordenado por cercanía' : 'Cerca de mí'}
        </button>
        {locError && <span className="text-xs text-error">{locError}</span>}
      </div>

      {error && <p role="alert" className="alert" data-testid="explore-error">{error}</p>}
      {items === null && !error && <p className="text-on-surface-variant">Cargando…</p>}

      {items !== null && items.length === 0 && (
        <div className="card flex flex-col items-center text-center py-stack-xl" data-testid="explore-empty">
          <span className="material-symbols-outlined text-[40px] text-on-surface-variant/40">storefront</span>
          <p className="mt-stack-sm font-semibold">No hay negocios que coincidan.</p>
          <p className="text-sm text-on-surface-variant">Prueba con otro nombre o categoría.</p>
        </div>
      )}

      {items !== null && items.length > 0 && (
        <ul className="grid grid-cols-1 sm:grid-cols-2 gap-stack-md" data-testid="explore-list">
          {items.map(({ b, dist }) => (
            <li key={b.id} className="card !p-0 overflow-hidden flex flex-col" data-testid="explore-item">
              {/* Foto o placeholder con icono de categoría */}
              <div className="relative h-32 w-full bg-gradient-to-br from-primary-container/40 to-secondary-container/40 flex items-center justify-center">
                {b.photoUrl ? (
                  <img src={b.photoUrl} alt={b.name} className="h-full w-full object-cover" loading="lazy"
                    onError={(e) => { (e.currentTarget as HTMLImageElement).style.display = 'none' }} />
                ) : (
                  <span className="material-symbols-outlined text-[44px] text-primary/60">{categoryIcon(b.category)}</span>
                )}
                {b.category && (
                  <span className="absolute left-2 top-2 inline-flex items-center gap-1 rounded-full bg-surface/90 px-2 py-0.5 text-[11px] font-bold text-on-surface backdrop-blur" data-testid="explore-category">
                    <span className="material-symbols-outlined text-[14px]">{categoryIcon(b.category)}</span>
                    {categoryLabel(b.category)}
                  </span>
                )}
                {dist != null && (
                  <span className="absolute right-2 top-2 rounded-full bg-surface/90 px-2 py-0.5 text-[11px] font-bold text-on-surface backdrop-blur" data-testid="explore-distance">
                    {formatDistance(dist)}
                  </span>
                )}
              </div>
              <div className="flex items-center gap-stack-md p-stack-md">
                <button type="button" onClick={() => setSelected(b)} className="min-w-0 flex-1 text-left group" data-testid="explore-details">
                  <p className="truncate font-bold group-hover:text-primary transition-colors">{b.name}</p>
                  <div className="mt-0.5" data-testid="explore-rating">
                    <RatingStars value={b.rating} count={b.reviewCount} />
                  </div>
                </button>
                {b.bookingMode === 'calendar_only' ? (
                  <span className="inline-flex items-center gap-1 rounded-full bg-surface-container px-3 py-2 text-xs font-semibold text-on-surface-variant shrink-0" data-testid="explore-in-person" title="Este negocio no acepta reservas online">
                    <span className="material-symbols-outlined text-[16px]">storefront</span>
                    Cita en persona
                  </span>
                ) : (
                  <Link to={`/reservar?businessId=${b.id}`} className="btn-primary py-2 text-sm shrink-0" data-testid="explore-reserve">
                    Reservar
                  </Link>
                )}
              </div>
            </li>
          ))}
        </ul>
      )}

      {selected && <BusinessDetailsModal business={selected} onClose={() => setSelected(null)} />}
    </section>
  )
}

/** Detalles de un negocio: foto, valoración y, sobre todo, cómo contactar/llegar. */
function BusinessDetailsModal({ business: b, onClose }: { business: BusinessResponse; onClose: () => void }) {
  const mapUrl = b.latitude != null && b.longitude != null
    ? `https://www.google.com/maps/search/?api=1&query=${b.latitude},${b.longitude}`
    : null
  const calendarOnly = b.bookingMode === 'calendar_only'

  return (
    <div
      className="fixed inset-0 z-50 flex items-end sm:items-center justify-center p-4 bg-black/40 backdrop-blur-sm"
      onClick={(e) => e.target === e.currentTarget && onClose()}
    >
      <div className="card w-full max-w-sm !p-0 overflow-hidden flex flex-col" data-testid="business-modal">
        <div className="relative h-36 w-full bg-gradient-to-br from-primary-container/40 to-secondary-container/40 flex items-center justify-center">
          {b.photoUrl
            ? <img src={b.photoUrl} alt={b.name} className="h-full w-full object-cover" onError={(e) => { (e.currentTarget as HTMLImageElement).style.display = 'none' }} />
            : <span className="material-symbols-outlined text-[48px] text-primary/60">{categoryIcon(b.category)}</span>}
          <button type="button" onClick={onClose} aria-label="Cerrar" data-testid="business-modal-close"
            className="absolute right-2 top-2 flex h-8 w-8 items-center justify-center rounded-full bg-surface/90 text-on-surface backdrop-blur hover:bg-surface">
            <span className="material-symbols-outlined text-[20px]">close</span>
          </button>
        </div>

        <div className="flex flex-col gap-stack-sm p-stack-md">
          <div>
            <h2 className="text-lg font-bold">{b.name}</h2>
            <div className="mt-0.5 flex items-center gap-2">
              {b.category && <span className="text-xs font-semibold text-on-surface-variant">{categoryLabel(b.category)}</span>}
              <RatingStars value={b.rating} count={b.reviewCount} />
            </div>
          </div>

          {calendarOnly && (
            <p className="rounded-lg bg-surface-container px-3 py-2 text-xs font-semibold text-on-surface-variant" data-testid="business-modal-in-person">
              Este negocio no reserva online. Contacta para tu cita:
            </p>
          )}

          {/* Contacto */}
          <div className="flex flex-col gap-1 text-sm">
            {b.phone ? (
              <a href={`tel:${b.phone}`} className="inline-flex items-center gap-2 font-semibold text-primary hover:underline" data-testid="business-modal-phone">
                <span className="material-symbols-outlined text-[18px]">call</span>{b.phone}
              </a>
            ) : <p className="inline-flex items-center gap-2 text-on-surface-variant"><span className="material-symbols-outlined text-[18px]">call</span>Sin teléfono</p>}
            {b.address && (
              <p className="inline-flex items-center gap-2 text-on-surface-variant" data-testid="business-modal-address">
                <span className="material-symbols-outlined text-[18px]">location_on</span>{b.address}
              </p>
            )}
            {mapUrl && (
              <a href={mapUrl} target="_blank" rel="noreferrer" className="inline-flex items-center gap-2 text-primary hover:underline">
                <span className="material-symbols-outlined text-[18px]">map</span>Ver en el mapa
              </a>
            )}
          </div>

          {!calendarOnly && (
            <Link to={`/reservar?businessId=${b.id}`} className="btn-primary text-center" data-testid="business-modal-reserve">
              Reservar
            </Link>
          )}
        </div>
      </div>
    </div>
  )
}
