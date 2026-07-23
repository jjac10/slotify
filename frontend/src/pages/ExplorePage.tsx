import { useEffect, useMemo, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { businessService } from '../services/businessService'
import { getApiError } from '../services/apiClient'
import { BUSINESS_CATEGORIES, categoryIcon, categoryLabel } from '../constants/categories'
import { RatingStars } from '../components/Stars'
import { WeeklyHours } from '../components/WeeklyHours'
import type { AvailableSlot, BusinessHour, BusinessResponse, ReviewResponse, ServiceResponse, StaffMember } from '../types/api'

function formatPrice(price: number | null): string {
  if (price === null) return 'Gratis'
  return new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR' }).format(price)
}

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

/** Tamaño de página del listado (el backend clampa a un máximo de 50). */
const PAGE_SIZE = 20

export function ExplorePage() {
  const [query, setQuery] = useState('')
  const [category, setCategory] = useState<string | null>(null)
  const [businesses, setBusinesses] = useState<BusinessResponse[] | null>(null)
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [loadingMore, setLoadingMore] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [coords, setCoords] = useState<Coords | null>(null)
  const [locating, setLocating] = useState(false)
  const [locError, setLocError] = useState<string | null>(null)
  const [selected, setSelected] = useState<BusinessResponse | null>(null)

  // Clave de la búsqueda vigente: descarta respuestas de "Cargar más" que lleguen
  // después de haber cambiado el texto o la categoría (se resetea a página 1).
  const searchKey = `${query.trim()}|${category ?? ''}`
  const searchKeyRef = useRef(searchKey)
  searchKeyRef.current = searchKey

  useEffect(() => {
    let active = true
    const handle = setTimeout(() => {
      businessService
        .searchPublic(query.trim() || undefined, category ?? undefined, 1, PAGE_SIZE)
        .then((data) => {
          if (active) { setBusinesses(data.items); setTotal(data.total); setPage(1); setError(null) }
        })
        .catch((err) => { if (active) setError(getApiError(err)?.message ?? 'No se pudieron cargar los negocios.') })
    }, 250)
    return () => { active = false; clearTimeout(handle) }
  }, [query, category])

  function loadMore() {
    const key = searchKey
    setLoadingMore(true)
    businessService
      .searchPublic(query.trim() || undefined, category ?? undefined, page + 1, PAGE_SIZE)
      .then((data) => {
        if (searchKeyRef.current !== key) return // la búsqueda cambió mientras cargaba
        setBusinesses((prev) => [...(prev ?? []), ...data.items])
        setTotal(data.total)
        setPage(data.page)
        setError(null)
      })
      .catch((err) => setError(getApiError(err)?.message ?? 'No se pudieron cargar más negocios.'))
      .finally(() => setLoadingMore(false))
  }

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
  // Decisión (paginación): la ordenación "cerca de mí" se mantiene en cliente y solo
  // reordena los resultados YA cargados; las coordenadas del usuario nunca se envían
  // al servidor (privacidad) y el orden estable entre páginas lo da el backend
  // (nombre + id). Ordenar por distancia en BD exigiría mandar lat/lng y haversine
  // en SQL — fuera del alcance de este slice.
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
            <li key={b.id} className="card !p-0 overflow-hidden flex flex-col relative group cursor-pointer" data-testid="explore-item">
              {/* Overlay clicable: pulsar en cualquier parte de la tarjeta abre el modal de detalles.
                  El botón "Reservar" se eleva por encima (z-10) para seguir siendo clicable por su cuenta. */}
              <button
                type="button"
                onClick={() => setSelected(b)}
                className="absolute inset-0 z-10"
                data-testid="explore-details"
                aria-label={`Ver detalles de ${b.name}`}
              />
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
                <div className="min-w-0 flex-1">
                  <p className="truncate font-bold group-hover:text-primary transition-colors">{b.name}</p>
                  <div className="mt-0.5" data-testid="explore-rating">
                    <RatingStars value={b.rating} count={b.reviewCount} />
                  </div>
                </div>
                {b.bookingMode === 'calendar_only' ? (
                  <span className="inline-flex items-center gap-1 rounded-full bg-surface-container px-3 py-2 text-xs font-semibold text-on-surface-variant shrink-0" data-testid="explore-in-person" title="Este negocio no acepta reservas online">
                    <span className="material-symbols-outlined text-[16px]">storefront</span>
                    Cita en persona
                  </span>
                ) : (
                  <Link to={`/reservar?businessId=${b.id}`} onClick={(e) => e.stopPropagation()} className="btn-primary py-2 text-sm shrink-0 relative z-20" data-testid="explore-reserve">
                    Reservar
                  </Link>
                )}
              </div>
            </li>
          ))}
        </ul>
      )}

      {/* Cargar más: mientras haya resultados sin traer (items.length < total) */}
      {businesses !== null && businesses.length < total && (
        <div className="mt-stack-md flex justify-center">
          <button
            type="button"
            onClick={loadMore}
            disabled={loadingMore}
            data-testid="explore-load-more"
            className="inline-flex items-center gap-1 rounded-full border border-outline-variant px-4 py-2 text-sm font-semibold text-on-surface-variant hover:bg-surface-container-low disabled:opacity-60"
          >
            <span className="material-symbols-outlined text-[18px]">expand_more</span>
            {loadingMore ? 'Cargando…' : `Cargar más (${businesses.length} de ${total})`}
          </button>
        </div>
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
  const [services, setServices] = useState<ServiceResponse[] | null>(null)
  const [staff, setStaff] = useState<StaffMember[] | null>(null)
  // Solo-calendario: el cliente ve el horario semanal y los huecos de hoy (solo
  // lectura) para saber cuándo hay sitio antes de llamar.
  const [weeklyHours, setWeeklyHours] = useState<BusinessHour[] | null>(null)
  const [todaySlots, setTodaySlots] = useState<AvailableSlot[] | null>(null)

  useEffect(() => {
    let active = true
    businessService.listServices(b.id).then((s) => active && setServices(s)).catch(() => active && setServices([]))
    businessService.listStaff(b.id).then((s) => active && setStaff(s)).catch(() => active && setStaff([]))
    return () => { active = false }
  }, [b.id])

  useEffect(() => {
    if (!calendarOnly) return
    let active = true
    businessService.getHours(b.id).then((h) => active && setWeeklyHours(h)).catch(() => active && setWeeklyHours([]))
    return () => { active = false }
  }, [b.id, calendarOnly])

  // Huecos libres de HOY (orientativos): primer servicio + primer profesional.
  useEffect(() => {
    if (!calendarOnly || !services?.length || !staff?.length) return
    let active = true
    const now = new Date()
    const today = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`
    businessService
      .availability(b.id, { serviceId: services[0].id, staffId: staff[0].id, date: today })
      .then((s) => active && setTodaySlots(s))
      .catch(() => active && setTodaySlots([]))
    return () => { active = false }
  }, [b.id, calendarOnly, services, staff])

  return (
    <div
      className="fixed inset-0 z-50 flex items-end sm:items-center justify-center p-4 bg-black/40 backdrop-blur-sm"
      onClick={(e) => e.target === e.currentTarget && onClose()}
    >
      <div className="card w-full max-w-sm !p-0 overflow-hidden flex flex-col" data-testid="business-modal">
        {/* Banda superior: foto si la hay; si no, el color de marca del negocio (o el degradado por defecto). */}
        <div className="relative h-36 w-full bg-gradient-to-br from-primary-container/40 to-secondary-container/40 flex items-center justify-center"
          style={!b.photoUrl && b.brandColor ? { backgroundImage: 'none', backgroundColor: b.brandColor } : undefined}
          data-testid="business-modal-band">
          {b.photoUrl
            ? <img src={b.photoUrl} alt={b.name} className="h-full w-full object-cover" onError={(e) => { (e.currentTarget as HTMLImageElement).style.display = 'none' }} />
            : <span className={`material-symbols-outlined text-[48px] ${b.brandColor ? 'text-white/80' : 'text-primary/60'}`}>{categoryIcon(b.category)}</span>}
          <button type="button" onClick={onClose} aria-label="Cerrar" data-testid="business-modal-close"
            className="absolute right-2 top-2 flex h-8 w-8 items-center justify-center rounded-full bg-surface/90 text-on-surface backdrop-blur hover:bg-surface">
            <span className="material-symbols-outlined text-[20px]">close</span>
          </button>
        </div>

        <div className="flex flex-col gap-stack-sm p-stack-md max-h-[60vh] overflow-y-auto">
          <div>
            <div className="flex items-center gap-2">
              {b.logoUrl && (
                <img src={b.logoUrl} alt="" data-testid="business-modal-logo"
                  className="h-9 w-9 shrink-0 rounded-full object-cover ring-1 ring-outline-variant/50"
                  style={b.brandColor ? { boxShadow: `0 0 0 2px ${b.brandColor}` } : undefined}
                  onError={(e) => { (e.currentTarget as HTMLImageElement).style.display = 'none' }} />
              )}
              <h2 className="text-lg font-bold">{b.name}</h2>
            </div>
            <div className="mt-0.5 flex items-center gap-2">
              {b.category && <span className="text-xs font-semibold text-on-surface-variant">{categoryLabel(b.category)}</span>}
              <RatingStars value={b.rating} count={b.reviewCount} />
            </div>
            {b.description && (
              <p className="mt-2 text-sm text-on-surface-variant" data-testid="business-modal-description">
                {b.description}
              </p>
            )}
          </div>

          {calendarOnly && (
            <p className="rounded-lg bg-surface-container px-3 py-2 text-xs font-semibold text-on-surface-variant" data-testid="business-modal-in-person">
              Este negocio no reserva online. Contacta para tu cita:
            </p>
          )}

          {/* Solo-calendario: horario semanal + huecos de hoy (solo lectura) */}
          {calendarOnly && weeklyHours !== null && weeklyHours.length > 0 && (
            <div className="border-t border-outline-variant/30 pt-stack-sm" data-testid="business-modal-hours">
              <p className="mb-1 text-xs font-bold uppercase tracking-wide text-on-surface-variant">Horario</p>
              <WeeklyHours hours={weeklyHours} />
            </div>
          )}
          {calendarOnly && todaySlots !== null && (
            <div className="border-t border-outline-variant/30 pt-stack-sm" data-testid="business-modal-today-slots">
              <p className="mb-1 text-xs font-bold uppercase tracking-wide text-on-surface-variant">Huecos hoy</p>
              {todaySlots.length === 0 ? (
                <p className="text-sm text-on-surface-variant">Hoy ya no quedan huecos — consulta el horario y llama.</p>
              ) : (
                <div className="flex flex-wrap gap-1.5">
                  {todaySlots.slice(0, 8).map((s) => (
                    <span key={s.start} className="rounded-full bg-surface-container px-2.5 py-1 text-xs font-semibold">
                      {new Date(s.start).toLocaleTimeString('es-ES', { hour: '2-digit', minute: '2-digit' })}
                    </span>
                  ))}
                  {todaySlots.length > 8 && (
                    <span className="px-1 py-1 text-xs text-on-surface-variant">+{todaySlots.length - 8} más</span>
                  )}
                </div>
              )}
              <p className="mt-1 text-[11px] text-on-surface-variant/70">
                Huecos orientativos{services?.[0] ? ` para «${services[0].name}»` : ''} — llama para confirmar tu cita.
              </p>
            </div>
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
            {b.website && (
              <a href={b.website} target="_blank" rel="noreferrer" data-testid="business-modal-website"
                className="inline-flex items-center gap-2 text-primary hover:underline">
                <span className="material-symbols-outlined text-[18px]">language</span>
                {b.website.replace(/^https?:\/\//, '')}
              </a>
            )}
            {b.instagram && (
              <a href={`https://instagram.com/${b.instagram}`} target="_blank" rel="noreferrer" data-testid="business-modal-instagram"
                className="inline-flex items-center gap-2 text-primary hover:underline">
                <span className="material-symbols-outlined text-[18px]">photo_camera</span>
                @{b.instagram}
              </a>
            )}
          </div>

          {/* Servicios (nombre · duración · precio) */}
          {services !== null && services.length > 0 && (
            <div className="border-t border-outline-variant/30 pt-stack-sm" data-testid="business-modal-services">
              <p className="mb-1 text-xs font-bold uppercase tracking-wide text-on-surface-variant">Servicios</p>
              <ul className="flex flex-col gap-1">
                {services.map((s) => (
                  <li key={s.id} className="flex items-center justify-between gap-2 text-sm">
                    <span className="min-w-0 truncate">
                      {s.name}
                      <span className="text-on-surface-variant"> · {s.durationMinutes} min</span>
                    </span>
                    <span className="shrink-0 font-semibold">{formatPrice(s.price)}</span>
                  </li>
                ))}
              </ul>
            </div>
          )}

          {/* Equipo */}
          {staff !== null && staff.length > 0 && (
            <div className="border-t border-outline-variant/30 pt-stack-sm" data-testid="business-modal-staff">
              <p className="mb-1 text-xs font-bold uppercase tracking-wide text-on-surface-variant">Equipo</p>
              <div className="flex flex-wrap gap-2">
                {staff.map((m) => (
                  <span key={m.id} className="inline-flex items-center gap-1.5 rounded-full bg-surface-container px-2.5 py-1 text-xs font-medium">
                    <span className="flex h-5 w-5 items-center justify-center rounded-full bg-primary-container/30 text-[10px] font-bold text-primary">
                      {m.name[0]?.toUpperCase() ?? '?'}
                    </span>
                    {m.name}
                  </span>
                ))}
              </div>
            </div>
          )}

          {/* Reseñas (paginadas en servidor, de más reciente a más antigua) */}
          {(b.reviewCount ?? 0) > 0 && <BusinessReviews businessId={b.id} />}

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

const REVIEWS_PAGE_SIZE = 5

/** Reseñas públicas del negocio dentro de su ficha, con "Ver más" acumulando páginas. */
function BusinessReviews({ businessId }: { businessId: string }) {
  const [items, setItems] = useState<ReviewResponse[]>([])
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [loading, setLoading] = useState(false)

  useEffect(() => {
    let active = true
    setLoading(true)
    businessService
      .listReviews(businessId, { page, pageSize: REVIEWS_PAGE_SIZE })
      .then((data) => {
        if (!active) return
        setItems((prev) => (page === 1 ? data.items : [...prev, ...data.items]))
        setTotal(data.total)
      })
      .catch(() => { /* sin reseñas visibles; la ficha sigue siendo útil */ })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [businessId, page])

  if (items.length === 0) return null

  return (
    <div className="border-t border-outline-variant/30 pt-stack-sm" data-testid="business-modal-reviews">
      <p className="mb-1 text-xs font-bold uppercase tracking-wide text-on-surface-variant">Reseñas</p>
      <ul className="flex flex-col gap-stack-sm">
        {items.map((r) => (
          <li key={r.id} className="rounded-lg bg-surface-container/60 px-3 py-2" data-testid="business-review-item">
            <div className="flex items-center justify-between gap-2">
              <span className="min-w-0 truncate text-xs font-bold">{r.authorName ?? 'Cliente'}</span>
              <RatingStars value={r.rating} size={13} />
            </div>
            {r.comment && <p className="mt-1 text-sm text-on-surface-variant">{r.comment}</p>}
            <p className="mt-0.5 text-[11px] text-on-surface-variant/70">
              {new Date(r.createdAt).toLocaleDateString('es-ES', { day: 'numeric', month: 'long', year: 'numeric' })}
            </p>
          </li>
        ))}
      </ul>
      {items.length < total && (
        <button
          type="button"
          className="mt-stack-sm text-sm font-semibold text-primary hover:underline disabled:opacity-50"
          data-testid="load-more-reviews"
          disabled={loading}
          onClick={() => setPage((p) => p + 1)}
        >
          {loading ? 'Cargando…' : `Ver más reseñas (${items.length} de ${total})`}
        </button>
      )}
    </div>
  )
}
