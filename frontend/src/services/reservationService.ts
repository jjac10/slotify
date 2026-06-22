import { api } from './apiClient'
import type { CreateReservationRequest, CreateReviewRequest, ReservationResponse, ReviewResponse } from '../types/api'

interface BusinessReservationsQuery {
  date?: string
  staffId?: string
}

export const reservationService = {
  /** GET /reservations/mine — reservas del usuario autenticado. */
  async listMine(): Promise<ReservationResponse[]> {
    const { data } = await api.get<ReservationResponse[]>('/reservations/mine')
    return data
  },

  /** POST /reservations/lookup — reservas de un invitado por teléfono o email (en el body, no en la URL). */
  async lookupGuest(contact: string): Promise<ReservationResponse[]> {
    const { data } = await api.post<ReservationResponse[]>('/reservations/lookup', { contact })
    return data
  },

  /** POST /reservations — crea una reserva (usuario logueado o invitado). */
  async create(request: CreateReservationRequest): Promise<ReservationResponse> {
    const { data } = await api.post<ReservationResponse>('/reservations', request)
    return data
  },

  /** GET /businesses/{id}/reservations — agenda del negocio (owner/staff). */
  async listForBusiness(
    businessId: string,
    query: BusinessReservationsQuery = {},
  ): Promise<ReservationResponse[]> {
    const { data } = await api.get<ReservationResponse[]>(
      `/businesses/${businessId}/reservations`,
      { params: query },
    )
    return data
  },

  /** PATCH /reservations/{id} — reprograma conservando la duración. Si `contact` se pasa, actúa como invitado. */
  async reschedule(id: string, startTime: string, contact?: string): Promise<ReservationResponse> {
    const { data } = await api.patch<ReservationResponse>(`/reservations/${id}`, {
      startTime,
      ...(contact ? { contact } : {}),
    })
    return data
  },

  /** POST /reservations/{id}/cancel — cancela (motivo/contacto en el body, no en la URL). Si `contact` se pasa, actúa como invitado. */
  async cancel(id: string, reason?: string, contact?: string): Promise<void> {
    await api.post(`/reservations/${id}/cancel`, {
      ...(reason ? { reason } : {}),
      ...(contact ? { contact } : {}),
    })
  },

  /** POST /reservations/{id}/confirm — confirma una reserva pending (owner/staff). */
  async confirm(id: string): Promise<ReservationResponse> {
    const { data } = await api.post<ReservationResponse>(`/reservations/${id}/confirm`)
    return data
  },

  /** POST /reservations/{id}/review — valora una reserva pasada propia (1–5 + comentario). */
  async review(id: string, request: CreateReviewRequest): Promise<ReviewResponse> {
    const { data } = await api.post<ReviewResponse>(`/reservations/${id}/review`, request)
    return data
  },
}
