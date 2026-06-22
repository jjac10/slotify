import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { reviewService } from '../services/reviewService'
import { getApiError } from '../services/apiClient'
import { RatingStars } from '../components/Stars'
import { ReviewModal } from '../components/ReviewModal'
import type { MyReviewResponse } from '../types/api'

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('es-ES', { day: 'numeric', month: 'long', year: 'numeric' })
}

/**
 * "Mis reseñas": las valoraciones del cliente registrado, con el negocio. Puede editarlas
 * (una reseña por negocio). Base para, en el futuro, ver las respuestas del negocio.
 */
export function MyReviewsPage() {
  const [reviews, setReviews] = useState<MyReviewResponse[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [editing, setEditing] = useState<MyReviewResponse | null>(null)

  function load() {
    reviewService.listMine()
      .then(setReviews)
      .catch((err) => setError(getApiError(err)?.message ?? 'No se pudieron cargar tus reseñas.'))
  }

  useEffect(load, [])

  return (
    <section>
      <h1>Mis reseñas</h1>
      <p className="text-on-surface-variant mb-stack-md">Tus valoraciones de los negocios donde has reservado.</p>

      {error && <p role="alert" className="alert" data-testid="my-reviews-error">{error}</p>}
      {reviews === null && !error && <p className="text-on-surface-variant">Cargando…</p>}

      {reviews !== null && reviews.length === 0 && (
        <div className="card flex flex-col items-center text-center py-stack-xl" data-testid="my-reviews-empty">
          <span className="material-symbols-outlined text-[40px] text-on-surface-variant/40">reviews</span>
          <p className="mt-stack-sm font-semibold">Aún no has dejado ninguna reseña.</p>
          <p className="text-sm text-on-surface-variant">Valora un negocio desde tus reservas pasadas en{' '}
            <Link to="/mis-reservas" className="font-semibold text-primary hover:underline">Mis reservas</Link>.
          </p>
        </div>
      )}

      {reviews !== null && reviews.length > 0 && (
        <ul className="flex flex-col gap-stack-sm" data-testid="my-reviews-list">
          {reviews.map((rv) => (
            <li key={rv.id} className="card flex flex-col gap-stack-sm" data-testid="my-review-item">
              <div className="flex items-start justify-between gap-stack-md">
                <div className="min-w-0">
                  <p className="font-bold truncate">{rv.businessName}</p>
                  <div className="mt-0.5"><RatingStars value={rv.rating} /></div>
                </div>
                <button
                  type="button"
                  onClick={() => setEditing(rv)}
                  className="flex items-center gap-1 rounded-lg px-3 py-1.5 text-xs font-semibold text-primary hover:bg-primary-container/15 transition-colors shrink-0"
                  data-testid="my-review-edit-btn"
                >
                  <span className="material-symbols-outlined text-[16px]">edit</span>
                  Editar
                </button>
              </div>
              {rv.comment && <p className="text-sm text-on-surface-variant">{rv.comment}</p>}
              <p className="text-xs text-on-surface-variant">
                {formatDate(rv.createdAt)}{rv.updatedAt ? ' · editada' : ''}
              </p>
            </li>
          ))}
        </ul>
      )}

      {editing && (
        <ReviewModal
          businessName={editing.businessName}
          reviewId={editing.id}
          initialRating={editing.rating}
          initialComment={editing.comment}
          onClose={() => setEditing(null)}
          onSaved={() => { load(); setEditing(null) }}
        />
      )}
    </section>
  )
}
