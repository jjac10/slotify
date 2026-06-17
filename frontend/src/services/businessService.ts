import { api } from './apiClient'
import type { AvailableSlot, BusinessResponse, ServiceResponse } from '../types/api'

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

  /** GET /businesses/{id}/services — servicios de un negocio (público). */
  async listServices(businessId: string): Promise<ServiceResponse[]> {
    const { data } = await api.get<ServiceResponse[]>(`/businesses/${businessId}/services`)
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
}
