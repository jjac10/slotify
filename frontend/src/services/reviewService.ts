import { api } from './apiClient'
import type { MyReviewResponse, UpdateReviewRequest } from '../types/api'

/** Reseñas propias del cliente (listar + editar). Crear va por reservationService.review. */
export const reviewService = {
  /** GET /me/reviews — reseñas propias del cliente, con el nombre del negocio. */
  async listMine(): Promise<MyReviewResponse[]> {
    const { data } = await api.get<MyReviewResponse[]>('/me/reviews')
    return data
  },

  /** PUT /reviews/{id} — edita una reseña propia. */
  async update(reviewId: string, request: UpdateReviewRequest): Promise<MyReviewResponse> {
    const { data } = await api.put<MyReviewResponse>(`/reviews/${reviewId}`, request)
    return data
  },
}
