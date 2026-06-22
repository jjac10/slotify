import { useState } from 'react'
import type { FormEvent } from 'react'
import { reservationService } from '../services/reservationService'
import { getApiError } from '../services/apiClient'
import { StarInput } from './Stars'
import type { ReservationResponse } from '../types/api'

interface Props {
  reservation: ReservationResponse
  onClose: () => void
  /** Se llama tras valorar con éxito (o si ya estaba valorada): marca la reserva como valorada. */
  onReviewed: (reservationId: string) => void
}

export function ReviewModal({ reservation, onClose, onReviewed }: Props) {
  const [rating, setRating] = useState(0)
  const [comment, setComment] = useState('')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (rating < 1) { setError('Elige cuántas estrellas (1–5).'); return }
    setError(null)
    setSaving(true)
    try {
      await reservationService.review(reservation.id, { rating, comment: comment.trim() || null })
      onReviewed(reservation.id)
    } catch (err) {
      const apiErr = getApiError(err)
      if (apiErr?.error === 'already_reviewed') {
        // Ya estaba valorada (p. ej. en otra pestaña): trátalo como hecho.
        onReviewed(reservation.id)
        return
      }
      setError(apiErr?.message ?? 'No se pudo enviar la valoración.')
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
          <h2 className="text-lg font-bold">Valorar tu cita</h2>
          <button type="button" onClick={onClose} className="p-1 rounded-lg hover:bg-surface-container-low" aria-label="Cerrar">
            <span className="material-symbols-outlined text-[22px] text-on-surface-variant">close</span>
          </button>
        </div>

        <p className="text-sm text-on-surface-variant -mt-stack-sm">
          <span className="font-medium text-on-surface">{reservation.businessName ?? 'Reserva'}</span>
          {reservation.serviceName ? ` · ${reservation.serviceName}` : ''}
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
          {saving ? 'Enviando…' : 'Enviar valoración'}
        </button>
      </form>
    </div>
  )
}
