import { api } from './apiClient'
import type {
  AuthResult,
  LoginRequest,
  MeResponse,
  RegisterCustomerRequest,
  RegisterOwnerRequest,
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
}
