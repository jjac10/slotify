import { api } from './apiClient'
import type {
  AuthResult,
  ForgotPasswordRequest,
  LoginRequest,
  MeResponse,
  RegisterCustomerRequest,
  RegisterOwnerRequest,
  ResetPasswordRequest,
  StaffInviteInfoResponse,
  VerifyEmailRequest,
} from '../types/api'

export const authService = {
  async login(request: LoginRequest): Promise<AuthResult> {
    const { data } = await api.post<AuthResult>('/auth/login', request)
    return data
  },

  async registerCustomer(request: RegisterCustomerRequest): Promise<AuthResult> {
    const { data } = await api.post<AuthResult>('/auth/register', request)
    return data
  },

  async registerOwner(request: RegisterOwnerRequest): Promise<AuthResult> {
    const { data } = await api.post<AuthResult>('/auth/register-owner', request)
    return data
  },

  async me(): Promise<MeResponse> {
    const { data } = await api.get<MeResponse>('/auth/me')
    return data
  },

  /** GET /auth/staff-invite/{token} — datos de una invitación de empleado pendiente. */
  async getStaffInvite(token: string): Promise<StaffInviteInfoResponse> {
    const { data } = await api.get<StaffInviteInfoResponse>(`/auth/staff-invite/${token}`)
    return data
  },

  /** POST /auth/staff-invite/{token}/accept — el empleado fija su contraseña y queda logueado. */
  async acceptStaffInvite(token: string, password: string): Promise<AuthResult> {
    const { data } = await api.post<AuthResult>(`/auth/staff-invite/${token}/accept`, { password })
    return data
  },

  /** POST /auth/forgot-password — el backend responde SIEMPRE 200 genérico (anti-enumeración). */
  async forgotPassword(request: ForgotPasswordRequest): Promise<void> {
    await api.post('/auth/forgot-password', request)
  },

  /** POST /auth/reset-password — restablece la contraseña con el token del email. */
  async resetPassword(request: ResetPasswordRequest): Promise<void> {
    await api.post('/auth/reset-password', request)
  },

  /** POST /auth/verify-email — verifica el email con el token del enlace (público). */
  async verifyEmail(request: VerifyEmailRequest): Promise<void> {
    await api.post('/auth/verify-email', request)
  },

  /** POST /auth/resend-verification — reenvía el enlace al usuario autenticado. */
  async resendVerification(): Promise<void> {
    await api.post('/auth/resend-verification')
  },
}
