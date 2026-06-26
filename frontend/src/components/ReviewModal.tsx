import { useState } from 'react'
import type { FormEvent } from 'react'
import { reservationService } from '../services/reservationService'
import { reviewService } from '../services/reviewService'
import { getApiError } from '../services/apiClient'
import { StarInput } from './Stars'

interface Props {
  /** Nombre del negocio que se valora (cabecera del modal). */
  businessName: string
  /** Servicio (opcional, solo informativo). */
  serviceName?: string | null
  /** Modo CREAR: id de la reserva pasada desde la que se valora. */
  reservationId?: string
  /** Modo EDITAR: id de la reseña existente. Tiene prioridad sobre reservationId. */
  reviewId?: string
  /** Valores iniciales (al editar). */
  initialRating?: number
  initialComment?: string | null
  onClose: () => void
  /** Se llama tras guardar con éxito (crear o editar). */
  onSaved: () => void
}

/**
 * Modal para valorar un negocio (1–5 + comentario). Crea (POST /reservations/{id}/review)
 * o, si llega <c>reviewId</c>, edita (PUT /reviews/{id}). Una reseña por negocio.
 */
export function ReviewModal({
  businessName, serviceName, reservationId, reviewId, initialRating = 0, initialComment, onClose, onSaved,
}: Props) {
  const isEdit = Boolean(reviewId)
  const [rating, setRating] = useState(initialRating)
  const [comment, setComment] = useState(initialComment ?? '')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (rating < 1) { setError('Elige cuántas estrellas (1–5).'); return }
    setError(null)
    setSaving(true)
    try {
      const body = { rating, comment: comment.trim() || null }
      if (reviewId) await reviewService.update(reviewId, body)
      else await reservationService.review(reservationId!, body)
      onSaved()
    } catch (err) {
      setError(getApiError(err)?.message ?? 'No se pudo guardar la valoración.')
      setSaving(false)
    }
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-end sm:items-center justify-center p-4 bg-black/40 backdrop-blur-sm"
      onClick={(e) => e.target === e.currentTarget && onClose()}
    >
      <form onSubmit={handleSubmit} className="card w-full max-w-sm flex flex-col gap-stack-md" data-testid="review-modal">
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-bold">{isEdit ? 'Editar tu valoración' : 'Valorar el negocio'}</h2>
          <button type="button" onClick={onClose} className="p-1 rounded-lg hover:bg-surface-container-low" aria-label="Cerrar">
            <span className="material-symbols-outlined text-[22px] text-on-surface-variant">close</span>
          </button>
        </div>

        <p className="text-sm text-on-surface-variant -mt-stack-sm">
          <span className="font-medium text-on-surface">{businessName}</span>
          {serviceName ? ` · ${serviceName}` : ''}
        </p>

        <div className="flex flex-col items-center gap-1">
          <StarInput value={rating} onChange={setRating} testId="review-star" />
          <span className="text-xs text-on-surface-variant">{rating > 0 ? `${rating} de 5` : 'Toca para puntuar'}</span>
        </div>

        <div className="field">
          <label className="field-label" htmlFor="review-comment">Comentario (opcional)</label>
          <textarea
            id="review-comment"
            className="field-input min-h-20 resize-y"
            data-testid="review-comment"
            value={comment}
            onChange={(e) => setComment(e.target.value)}
            maxLength={1000}
            placeholder="¿Qué tal fue tu experiencia?"
          />
        </div>

        {error && <p role="alert" className="alert text-sm" data-testid="review-error">{error}</p>}

        <button type="submit" className="btn-primary" data-testid="review-submit" disabled={saving || rating < 1}>
          {saving ? 'Guardando…' : isEdit ? 'Guardar cambios' : 'Enviar valoración'}
        </button>
      </form>
    </div>
  )
}
