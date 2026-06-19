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

  /** GET /reservations/lookup — reservas de un invitado por teléfono o email. */
  async lookupGuest(contact: string): Promise<ReservationResponse[]> {
    const { data } = await api.get<ReservationResponse[]>('/reservations/lookup', { params: { contact } })
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

  /** DELETE /reservations/{id} — cancela. Si `contact` se pasa, actúa como invitado. */
  async cancel(id: string, reason?: string, contact?: string): Promise<void> {
    const params: Record<string, string> = {}
    if (reason) params.reason = reason
    if (contact) params.contact = contact
    await api.delete(`/reservations/${id}`, {
      params: Object.keys(params).length ? params : undefined,
    })
  },

  /** POST /reservations/{id}/confirm — confirma una reserva pending (owner/staff). */
  async confirm(id: string): Promise<ReservationResponse> {
    const { data } = await api.post<ReservationResponse>(`/reservations/${id}/confirm`)
    return data
  },
}
