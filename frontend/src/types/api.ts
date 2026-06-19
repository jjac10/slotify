/**
 * Tipos del contrato de la API de Slotify.
 *
 * Reflejan los DTOs reales del backend (ASP.NET Core serializa en camelCase),
 * NO la versión aspiracional de docs/API.md. Mantener en sync con
 * backend/Slotify.Domain/DTOs.
 */

/** Resultado de register/login/refresh: identidad + tokens. `businessId` no nulo ⇒ owner. */
export interface AuthResult {
  userId: string
  businessId: string | null
  accessToken: string
  refreshToken: string
}

export interface RegisterCustomerRequest {
  email: string
  password: string
  name: string
  /** Opcional: vincula reservas previas hechas como invitado con ese teléfono. */
  phone?: string
}

export interface RegisterOwnerRequest {
  email: string
  password: string
  name: string
  businessName: string
}

export interface LoginRequest {
  email: string
  password: string
}

/** GET /auth/me */
export interface MeResponse {
  userId: string
  email: string
}

/** GET /businesses (negocios del owner autenticado). */
export interface BusinessResponse {
  id: string
  name: string
  status: string
  confirmationMode: string
  cancellationCutoffHours: number
}

/** GET /businesses/{id}/holidays — festivo/día cerrado. */
export interface BusinessHoliday {
  id: string
  holidayDate: string // "YYYY-MM-DD"
  reason: string | null
  isClosed: boolean
}

/** GET /businesses/{id}/services (público). */
export interface ServiceResponse {
  id: string
  businessId: string
  name: string
  description: string | null
  durationMinutes: number
  price: number | null
  color: string | null
  status: string
}

/** POST /businesses/{id}/services — alta de servicio (solo owner). */
export interface CreateServiceRequest {
  name: string
  description?: string | null
  durationMinutes: number
  price?: number | null
  color?: string | null
}

/**
 * Un día del horario semanal. `dayOfWeek`: 0=domingo … 6=sábado.
 * Las horas son `TimeOnly` serializadas como "HH:mm:ss" (null si el día está cerrado).
 */
export interface BusinessHour {
  dayOfWeek: number
  isClosed: boolean
  openingTime: string | null
  closingTime: string | null
}

/** PUT /businesses/{id}/hours — fija el horario semanal completo (solo owner). */
export interface SetBusinessHoursRequest {
  days: BusinessHour[]
}

/** Un hueco reservable. `start` se usa como `startTime` al crear la reserva (ISO UTC). */
export interface AvailableSlot {
  start: string
  end: string
}

/** ReservationResponse del backend. Fechas en ISO UTC. */
export interface ReservationResponse {
  id: string
  businessId: string
  serviceId: string
  staffId: string
  userId: string | null
  guestId: string | null
  startTime: string
  endTime: string
  status: string
  /** Nombres enriquecidos por el backend en los listados (null en la creación). */
  businessName: string | null
  serviceName: string | null
  staffName: string | null
}

/**
 * POST /reservations. Si la petición lleva JWT, el cliente es ese usuario;
 * si no, es invitado y debe venir `guestName` + exactamente uno de teléfono/email.
 * `staffId` es obligatorio.
 */
export interface CreateReservationRequest {
  businessId: string
  serviceId: string
  staffId: string
  startTime: string
  guestName?: string
  guestPhone?: string
  guestEmail?: string
}

/** GET /businesses/{id}/dashboard — resumen para el owner. */
export interface DashboardResponse {
  totalReservations: number
  reservationsThisMonth: number
  estimatedMonthlyRevenue: number
  upcomingReservations: ReservationResponse[]
}

/** Cuerpo de error estándar del backend. */
export interface ApiError {
  error: string
  message: string
  details?: Record<string, string[]>
}
