import { api } from './apiClient'
import type { PagedResponse } from '../types/api'

/** GET /admin/businesses — fila del directorio de moderación. */
export interface AdminBusinessResponse {
  id: string
  name: string
  ownerEmail: string
  createdAt: string
}

/**
 * Moderación de la plataforma (solo el admin configurado en el backend con
 * Admin:Email; el resto recibe 403). No da de alta negocios: elimina spam/prueba.
 */
export const adminService = {
  async listBusinesses(query: { q?: string; page?: number; pageSize?: number } = {}): Promise<PagedResponse<AdminBusinessResponse>> {
    const { data } = await api.get<PagedResponse<AdminBusinessResponse>>('/admin/businesses', { params: query })
    return data
  },

  /** Borrado en cascada de un negocio (moderación; irreversible). */
  async deleteBusiness(businessId: string): Promise<void> {
    await api.post(`/admin/businesses/${businessId}/delete`)
  },
}
