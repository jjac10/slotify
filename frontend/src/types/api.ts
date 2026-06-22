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
  /** Código del plan/tier: 'free' | 'premium' (null si la consulta no lo cargó). */
  plan: string | null
  /** Perfil público (Explorar). */
  category: string | null
  photoUrl: string | null
  latitude: number | null
  longitude: number | null
  /** Valoraciones (denormalizado). `rating` null si aún no tiene reseñas. */
  rating: number | null
  reviewCount: number
  /** Modo de reservas: 'online' (clientes reservan) | 'calendar_only' (solo el owner apunta). */
  bookingMode: string
}

/** GET /businesses/{id}/reviews — una reseña pública de un negocio. */
export interface ReviewResponse {
  id: string
  businessId: string
  reservationId: string
  rating: number
  comment: string | null
  authorName: string | null
  createdAt: string
}

/** POST /reservations/{id}/review — valora una reserva pasada propia (1–5 + comentario). */
export interface CreateReviewRequest {
  rating: number
  comment?: string | null
}

/** PUT /businesses/{id}/profile — perfil público del negocio. */
export interface UpdateBusinessProfileRequest {
  category: string | null
  photoUrl: string | null
  latitude: number | null
  longitude: number | null
}

/** GET /businesses/{id}/staff — trabajador del negocio (no expone email/teléfono). */
export interface StaffMember {
  id: string
  businessId: string
  name: string
  /** 'owner' | 'employee'. */
  role: string
  /** 'active' | 'inactive'. */
  status: string
}

/** POST /businesses/{id}/staff — alta de empleado (solo owner). */
export interface CreateStaffRequest {
  name: string
  email?: string | null
  phone?: string | null
}

/** GET /businesses/{id}/holidays — festivo/día(s) cerrado(s). */
export interface BusinessHoliday {
  id: string
  holidayDate: string // "YYYY-MM-DD" (primer día)
  /** Último día del rango (incl.); null = un solo día. */
  endDate: string | null
  /** Franja cerrada "HH:mm:ss"; null = día(s) completo(s). */
  startTime: string | null
  endTime: string | null
  reason: string | null
  isClosed: boolean
}

/** POST /businesses/{id}/holidays. */
export interface CreateHolidayRequest {
  holidayDate: string
  reason?: string | null
  isClosed?: boolean
  endDate?: string | null
  startTime?: string | null
  endTime?: string | null
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
  /** Nombre del cliente (invitado o usuario); enriquecido en la agenda del negocio. */
  clientName: string | null
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
  /** Valoraciones: media (null si no hay reseñas), nº y las más recientes. */
  averageRating: number | null
  reviewCount: number
  recentReviews: ReviewResponse[]
}

/** Cuerpo de error estándar del backend. */
export interface ApiError {
  error: string
  message: string
  details?: Record<string, string[]>
}
