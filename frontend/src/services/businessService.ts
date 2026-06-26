import { api } from './apiClient'
import type {
  AvailableSlot,
  BusinessHoliday,
  BusinessHour,
  BusinessResponse,
  CreateHolidayRequest,
  CreateServiceRequest,
  CreateStaffRequest,
  DashboardResponse,
  ReviewResponse,
  ServiceResponse,
  StaffInviteResponse,
  StaffMember,
  UpdateBusinessProfileRequest,
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

  /** GET /public/businesses — listado/búsqueda pública (por nombre y/o categoría). */
  async searchPublic(query?: string, category?: string): Promise<BusinessResponse[]> {
    const params: Record<string, string> = {}
    if (query) params.q = query
    if (category) params.category = category
    const { data } = await api.get<BusinessResponse[]>('/public/businesses', {
      params: Object.keys(params).length ? params : undefined,
    })
    return data
  },

  /** GET /businesses/{id}/reviews — reseñas públicas de un negocio (más recientes primero). */
  async listReviews(businessId: string): Promise<ReviewResponse[]> {
    const { data } = await api.get<ReviewResponse[]>(`/businesses/${businessId}/reviews`)
    return data
  },

  /** PUT /businesses/{id}/profile — perfil público (categoría/foto/ubicación; solo owner). */
  async updateProfile(businessId: string, request: UpdateBusinessProfileRequest): Promise<BusinessResponse> {
    const { data } = await api.put<BusinessResponse>(`/businesses/${businessId}/profile`, request)
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

  /** PUT /businesses/{id}/services/{serviceId} — edita un servicio (solo owner). */
  async updateService(businessId: string, serviceId: string, request: CreateServiceRequest): Promise<ServiceResponse> {
    const { data } = await api.put<ServiceResponse>(`/businesses/${businessId}/services/${serviceId}`, request)
    return data
  },

  /** DELETE /businesses/{id}/services/{serviceId} — elimina (archiva) un servicio (solo owner). */
  async deleteService(businessId: string, serviceId: string): Promise<void> {
    await api.delete(`/businesses/${businessId}/services/${serviceId}`)
  },

  /** GET /businesses/{id}/availability — huecos libres de un servicio + trabajador. */
  async availability(businessId: string, query: AvailabilityQuery): Promise<AvailableSlot[]> {
    const { data } = await api.get<AvailableSlot[]>(
      `/businesses/${businessId}/availability`,
      { params: query },
    )
    return data
  },

  /**
   * GET /businesses/{id}/staff — trabajadores activos del negocio (público).
   * Con `serviceId` filtra a los que pueden realizar ese servicio (un trabajador
   * sin servicios asignados se considera capaz de todos).
   */
  async listStaff(businessId: string, serviceId?: string): Promise<StaffMember[]> {
    const { data } = await api.get<StaffMember[]>(`/businesses/${businessId}/staff`, {
      params: serviceId ? { serviceId } : undefined,
    })
    return data
  },

  /** POST /businesses/{id}/staff — alta de empleado (solo owner; requiere Premium si Free está al límite). */
  async createStaff(businessId: string, request: CreateStaffRequest): Promise<StaffMember> {
    const { data } = await api.post<StaffMember>(`/businesses/${businessId}/staff`, {
      name: request.name,
      email: request.email || null,
      phone: request.phone || null,
    })
    return data
  },

  /** POST /businesses/{id}/staff/{staffId}/invite — genera el token de invitación del empleado (solo owner). */
  async inviteStaff(businessId: string, staffId: string): Promise<StaffInviteResponse> {
    const { data } = await api.post<StaffInviteResponse>(`/businesses/${businessId}/staff/${staffId}/invite`, {})
    return data
  },

  /** DELETE /businesses/{id}/staff/{staffId} — baja lógica de un empleado (solo owner). */
  async deactivateStaff(businessId: string, staffId: string): Promise<void> {
    await api.delete(`/businesses/${businessId}/staff/${staffId}`)
  },

  /** GET /businesses/{id}/staff/{staffId}/services — ids de servicios que hace el trabajador (solo owner). */
  async getStaffServices(businessId: string, staffId: string): Promise<string[]> {
    const { data } = await api.get<string[]>(`/businesses/${businessId}/staff/${staffId}/services`)
    return data
  },

  /** PUT /businesses/{id}/staff/{staffId}/services — fija los servicios del trabajador (vacío = todos; solo owner). */
  async setStaffServices(businessId: string, staffId: string, serviceIds: string[]): Promise<string[]> {
    const { data } = await api.put<string[]>(`/businesses/${businessId}/staff/${staffId}/services`, { serviceIds })
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

  /** POST /businesses/{id}/holidays — añade un festivo/cierre (día, rango y/o franja horaria). */
  async addHoliday(businessId: string, request: CreateHolidayRequest): Promise<BusinessHoliday> {
    const { data } = await api.post<BusinessHoliday>(`/businesses/${businessId}/holidays`, {
      holidayDate: request.holidayDate,
      reason: request.reason ?? null,
      isClosed: request.isClosed ?? true,
      endDate: request.endDate ?? null,
      startTime: request.startTime ?? null,
      endTime: request.endTime ?? null,
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

  /** PUT /businesses/{id}/booking-mode — cambia el modo de reservas (online|calendar_only). */
  async setBookingMode(businessId: string, mode: string): Promise<BusinessResponse> {
    const { data } = await api.put<BusinessResponse>(`/businesses/${businessId}/booking-mode`, { mode })
    return data
  },

  /** PUT /businesses/{id}/notification-settings — configura avisos (canales + recordatorio). */
  async setNotificationSettings(
    businessId: string,
    settings: { notifyByEmail: boolean; notifyByWhatsapp: boolean; reminderHoursBefore: number },
  ): Promise<BusinessResponse> {
    const { data } = await api.put<BusinessResponse>(`/businesses/${businessId}/notification-settings`, settings)
    return data
  },

  /** PUT /businesses/{id}/cancellation-cutoff — fija la ventana mínima de antelación (0 = sin límite). */
  async setCancellationCutoff(businessId: string, hours: number): Promise<BusinessResponse> {
    const { data } = await api.put<BusinessResponse>(`/businesses/${businessId}/cancellation-cutoff`, { hours })
    return data
  },

  /** PUT /businesses/{id}/plan — cambia el plan del negocio (free|premium). Upgrade simulado en el TFM. */
  async setPlan(businessId: string, code: string): Promise<BusinessResponse> {
    const { data } = await api.put<BusinessResponse>(`/businesses/${businessId}/plan`, { code })
    return data
  },
}
