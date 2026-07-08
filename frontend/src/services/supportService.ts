import { api } from './apiClient'

export interface ContactSupportRequest {
  name: string
  email: string
  message: string
}

/**
 * Soporte/contacto de la plataforma. `POST /support/contact` es público
 * (sin auth) y responde 204; los errores llegan como
 * `{ error: "invalid_contact_message", details: string[] }` (400) o
 * `{ error: "rate_limited" }` (429).
 */
export const supportService = {
  async sendContactMessage(request: ContactSupportRequest): Promise<void> {
    await api.post('/support/contact', request)
  },
}
