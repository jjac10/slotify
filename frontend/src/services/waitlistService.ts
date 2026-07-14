import { api } from './apiClient'

/** Entrada de lista de espera del usuario (GET /me/waitlist). */
export interface WaitlistEntryResponse {
  id: string
  businessId: string
  businessName: string | null
  serviceId: string
  serviceName: string | null
  /** "YYYY-MM-DD" (día local del negocio). */
  date: string
  position: number
  /** 'waiting' | 'notified'. */
  status: string
}

/**
 * Lista de espera (solo usuarios registrados): apuntarse a un (servicio, día)
 * completo; el backend avisa cuando una cancelación libera hueco.
 */
export const waitlistService = {
  /** 201 con la entrada; 409 already_waiting | slots_available. */
  async join(businessId: string, serviceId: string, date: string): Promise<WaitlistEntryResponse> {
    const { data } = await api.post<WaitlistEntryResponse>(
      `/businesses/${businessId}/waitlist`, { serviceId, date })
    return data
  },

  async listMine(): Promise<WaitlistEntryResponse[]> {
    const { data } = await api.get<WaitlistEntryResponse[]>('/me/waitlist')
    return data
  },

  async leave(entryId: string): Promise<void> {
    await api.delete(`/waitlist/${entryId}`)
  },
}
