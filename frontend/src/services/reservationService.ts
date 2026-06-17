import { api } from './apiClient'
import type { CreateReservationRequest, ReservationResponse } from '../types/api'

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

  /** PATCH /reservations/{id} — reprograma conservando la duración. */
  async reschedule(id: string, startTime: string): Promise<ReservationResponse> {
    const { data } = await api.patch<ReservationResponse>(`/reservations/${id}`, { startTime })
    return data
  },

  /** DELETE /reservations/{id} — cancela. */
  async cancel(id: string, reason?: string): Promise<void> {
    await api.delete(`/reservations/${id}`, {
      params: reason ? { reason } : undefined,
    })
  },
}
