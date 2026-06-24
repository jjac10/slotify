import { api } from './apiClient'
import type {
  AuthResult,
  LoginRequest,
  MeResponse,
  RegisterCustomerRequest,
  RegisterOwnerRequest,
  StaffInviteInfoResponse,
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
}
