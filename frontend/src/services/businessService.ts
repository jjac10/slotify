import { api } from './apiClient'
import type {
  AvailableSlot,
  BusinessHoliday,
  BusinessHour,
  BusinessResponse,
  CreateServiceRequest,
  DashboardResponse,
  ServiceResponse,
} from '../types/api'

interface AvailabilityQuery {
  serviceId: string
  staffId: string
  /** Fecha local del negocio en formato YYYY-MM-DD. */
  date: string
}

export const businessService = {
  /** GET /businesses — negocios del owner autenticado. */
  async listMine(): Promise<BusinessResponse[]> {
    const { data } = await api.get<BusinessResponse[]>('/businesses')
    return data
  },

  /** GET /public/businesses — listado/búsqueda pública de negocios (por nombre). */
  async searchPublic(query?: string): Promise<BusinessResponse[]> {
    const { data } = await api.get<BusinessResponse[]>('/public/businesses', {
      params: query ? { q: query } : undefined,
    })
    return data
  },

  /** GET /businesses/{id}/services — servicios de un negocio (público). */
  async listServices(businessId: string): Promise<ServiceResponse[]> {
    const { data } = await api.get<ServiceResponse[]>(`/businesses/${businessId}/services`)
    return data
  },

  /** POST /businesses/{id}/services — alta de servicio (solo owner). */
  async createService(businessId: string, request: CreateServiceRequest): Promise<ServiceResponse> {
    const { data } = await api.post<ServiceResponse>(`/businesses/${businessId}/services`, request)
    return data
  },

  /** GET /businesses/{id}/availability — huecos libres de un servicio + trabajador. */
  async availability(businessId: string, query: AvailabilityQuery): Promise<AvailableSlot[]> {
    const { data } = await api.get<AvailableSlot[]>(
      `/businesses/${businessId}/availability`,
      { params: query },
    )
    return data
  },

  /** GET /businesses/{id}/staff — trabajadores activos del negocio (público). */
  async listStaff(businessId: string): Promise<Array<{ id: string; name: string; role: string }>> {
    const { data } = await api.get(
      `/businesses/${businessId}/staff`,
    )
    return data
  },

  /** GET /businesses/{id}/dashboard — resumen del negocio (solo owner). */
  async dashboard(businessId: string): Promise<DashboardResponse> {
    const { data } = await api.get<DashboardResponse>(`/businesses/${businessId}/dashboard`)
    return data
  },

  /** GET /businesses/{id}/hours — horario semanal del negocio (público). */
  async getHours(businessId: string): Promise<BusinessHour[]> {
    const { data } = await api.get<BusinessHour[]>(`/businesses/${businessId}/hours`)
    return data
  },

  /** PUT /businesses/{id}/hours — fija el horario semanal completo (solo owner). */
  async setHours(businessId: string, days: BusinessHour[]): Promise<BusinessHour[]> {
    const { data } = await api.put<BusinessHour[]>(`/businesses/${businessId}/hours`, { days })
    return data
  },

  /** GET /businesses/{id}/holidays — lista los festivos del negocio. */
  async getHolidays(businessId: string): Promise<BusinessHoliday[]> {
    const { data } = await api.get<BusinessHoliday[]>(`/businesses/${businessId}/holidays`)
    return data
  },

  /** POST /businesses/{id}/holidays — añade un día festivo/cerrado. */
  async addHoliday(businessId: string, holidayDate: string, reason?: string): Promise<BusinessHoliday> {
    const { data } = await api.post<BusinessHoliday>(`/businesses/${businessId}/holidays`, {
      holidayDate,
      reason: reason || null,
      isClosed: true,
    })
    return data
  },

  /** DELETE /businesses/{id}/holidays/{holidayId} — elimina un festivo. */
  async removeHoliday(businessId: string, holidayId: string): Promise<void> {
    await api.delete(`/businesses/${businessId}/holidays/${holidayId}`)
  },

  /** PUT /businesses/{id}/confirmation-mode — cambia el modo de confirmación (auto|manual). */
  async setConfirmationMode(businessId: string, mode: string): Promise<BusinessResponse> {
    const { data } = await api.put<BusinessResponse>(`/businesses/${businessId}/confirmation-mode`, { mode })
    return data
  },

  /** PUT /businesses/{id}/cancellation-cutoff — fija la ventana mínima de antelación (0 = sin límite). */
  async setCancellationCutoff(businessId: string, hours: number): Promise<BusinessResponse> {
    const { data } = await api.put<BusinessResponse>(`/businesses/${businessId}/cancellation-cutoff`, { hours })
    return data
  },
}
