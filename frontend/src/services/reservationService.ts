import { api } from './apiClient'
import type {
  CreateReservationRequest,
  CreateReviewRequest,
  PagedResponse,
  ReservationResponse,
  ReservationScope,
  ReviewResponse,
} from '../types/api'

interface MyReservationsQuery {
  /** 'upcoming' | 'past' | 'all' (default del backend: 'all'). */
  scope?: ReservationScope
  /** 1-based (default del backend: 1). */
  page?: number
  /** Default del backend: 20; máx. 50. */
  pageSize?: number
}

interface BusinessReservationsQuery {
  date?: string
  staffId?: string
  /** 1-based (default del backend: 1). */
  page?: number
  /** Default del backend: 20; máx. 50. */
  pageSize?: number
}

export const reservationService = {
  /**
   * GET /reservations/mine — reservas del usuario autenticado, paginadas en servidor:
   * { items, total, page, pageSize }. `scope` acota por inicio (upcoming/past/all).
   */
  async listMine(query: MyReservationsQuery = {}): Promise<PagedResponse<ReservationResponse>> {
    const { data } = await api.get<PagedResponse<ReservationResponse>>('/reservations/mine', { params: query })
    return data
  },

  /** POST /reservations/lookup/otp — envía el código de verificación al contacto (204 siempre). */
  async requestGuestOtp(contact: string): Promise<void> {
    await api.post('/reservations/lookup/otp', { contact })
  },

  /**
   * POST /reservations/lookup — reservas de un invitado por teléfono o email (en el body,
   * no en la URL). Requiere el código OTP pedido antes; si no es válido → 403 invalid_otp.
   */
  async lookupGuest(contact: string, otpCode: string): Promise<ReservationResponse[]> {
    const { data } = await api.post<ReservationResponse[]>('/reservations/lookup', { contact, otpCode })
    return data
  },

  /** POST /reservations — crea una reserva (usuario logueado o invitado). */
  async create(request: CreateReservationRequest): Promise<ReservationResponse> {
    const { data } = await api.post<ReservationResponse>('/reservations', request)
    return data
  },

  /**
   * GET /businesses/{id}/reservations — agenda del negocio (owner/staff), paginada en
   * servidor: { items, total, page, pageSize }. Filtros opcionales por día (UTC) y trabajador.
   */
  async listForBusiness(
    businessId: string,
    query: BusinessReservationsQuery = {},
  ): Promise<PagedResponse<ReservationResponse>> {
    const { data } = await api.get<PagedResponse<ReservationResponse>>(
      `/businesses/${businessId}/reservations`,
      { params: query },
    )
    return data
  },

  /**
   * PATCH /reservations/{id} — reprograma conservando la duración. Si `contact` se pasa,
   * actúa como invitado y necesita también su `otpCode` vigente.
   */
  async reschedule(id: string, startTime: string, contact?: string, otpCode?: string): Promise<ReservationResponse> {
    const { data } = await api.patch<ReservationResponse>(`/reservations/${id}`, {
      startTime,
      ...(contact ? { contact } : {}),
      ...(otpCode ? { otpCode } : {}),
    })
    return data
  },

  /**
   * POST /reservations/{id}/cancel — cancela (motivo/contacto en el body, no en la URL).
   * Si `contact` se pasa, actúa como invitado y necesita también su `otpCode` vigente.
   */
  async cancel(id: string, reason?: string, contact?: string, otpCode?: string): Promise<void> {
    await api.post(`/reservations/${id}/cancel`, {
      ...(reason ? { reason } : {}),
      ...(contact ? { contact } : {}),
      ...(otpCode ? { otpCode } : {}),
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
