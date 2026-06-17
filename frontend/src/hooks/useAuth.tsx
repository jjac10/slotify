import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
} from 'react'
import type { ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import { authService } from '../services/authService'
import { registerUnauthorizedHandler } from '../services/apiClient'
import { tokenStorage } from '../services/tokenStorage'
import type {
  AuthResult,
  LoginRequest,
  RegisterCustomerRequest,
  RegisterOwnerRequest,
} from '../types/api'

export interface AuthUser {
  userId: string
  email: string
}

type AuthStatus = 'loading' | 'authenticated' | 'anonymous'

interface AuthContextValue {
  user: AuthUser | null
  businessId: string | null
  status: AuthStatus
  isOwner: boolean
  login: (request: LoginRequest) => Promise<void>
  registerCustomer: (request: RegisterCustomerRequest) => Promise<void>
  registerOwner: (request: RegisterOwnerRequest) => Promise<void>
  logout: () => void
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined)

export function AuthProvider({ children }: { children: ReactNode }) {
  const navigate = useNavigate()
  const [user, setUser] = useState<AuthUser | null>(null)
  const [businessId, setBusinessId] = useState<string | null>(tokenStorage.getBusinessId())
  const [status, setStatus] = useState<AuthStatus>(
    tokenStorage.getAccessToken() ? 'loading' : 'anonymous',
  )

  const clearSession = useCallback(() => {
    tokenStorage.clear()
    setUser(null)
    setBusinessId(null)
    setStatus('anonymous')
  }, [])

  const logout = useCallback(() => {
    clearSession()
    navigate('/login')
  }, [clearSession, navigate])

  // Sesión expirada (401 en petición autenticada) ⇒ cerrar sesión y mandar a login.
  useEffect(() => {
    registerUnauthorizedHandler(() => {
      clearSession()
      navigate('/login')
    })
    return () => registerUnauthorizedHandler(null)
  }, [clearSession, navigate])

  // Al cargar: si hay token guardado, hidratar el usuario desde /auth/me.
  useEffect(() => {
    if (!tokenStorage.getAccessToken()) return
    let active = true
    authService
      .me()
      .then((me) => {
        if (active) {
          setUser({ userId: me.userId, email: me.email })
          setStatus('authenticated')
        }
      })
      .catch(() => {
        if (active) clearSession()
      })
    return () => {
      active = false
    }
  }, [clearSession])

  const applySession = useCallback((result: AuthResult, email: string) => {
    tokenStorage.setSession(result)
    setUser({ userId: result.userId, email })
    setBusinessId(result.businessId)
    setStatus('authenticated')
  }, [])

  const login = useCallback(
    async (request: LoginRequest) => {
      applySession(await authService.login(request), request.email)
    },
    [applySession],
  )

  const registerCustomer = useCallback(
    async (request: RegisterCustomerRequest) => {
      applySession(await authService.registerCustomer(request), request.email)
    },
    [applySession],
  )

  const registerOwner = useCallback(
    async (request: RegisterOwnerRequest) => {
      applySession(await authService.registerOwner(request), request.email)
    },
    [applySession],
  )

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      businessId,
      status,
      isOwner: businessId !== null,
      login,
      registerCustomer,
      registerOwner,
      logout,
    }),
    [user, businessId, status, login, registerCustomer, registerOwner, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext)
  if (context === undefined) {
    throw new Error('useAuth debe usarse dentro de <AuthProvider>')
  }
  return context
}
