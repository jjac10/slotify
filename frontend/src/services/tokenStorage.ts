/**
 * Persistencia de la sesión en localStorage. Centraliza las claves para que
 * el resto del código no toque localStorage directamente.
 */

const ACCESS_KEY = 'slotify.accessToken'
const REFRESH_KEY = 'slotify.refreshToken'
const BUSINESS_KEY = 'slotify.businessId'
const ROLE_KEY = 'slotify.businessRole'

interface SessionTokens {
  accessToken: string
  refreshToken: string
  businessId: string | null
  /** 'owner' | 'staff' | null — distingue al dueño del empleado. */
  businessRole?: string | null
}

export const tokenStorage = {
  getAccessToken(): string | null {
    return localStorage.getItem(ACCESS_KEY)
  },

  getRefreshToken(): string | null {
    return localStorage.getItem(REFRESH_KEY)
  },

  getBusinessId(): string | null {
    return localStorage.getItem(BUSINESS_KEY)
  },

  getBusinessRole(): string | null {
    return localStorage.getItem(ROLE_KEY)
  },

  setSession(session: SessionTokens): void {
    localStorage.setItem(ACCESS_KEY, session.accessToken)
    localStorage.setItem(REFRESH_KEY, session.refreshToken)
    if (session.businessId) localStorage.setItem(BUSINESS_KEY, session.businessId)
    else localStorage.removeItem(BUSINESS_KEY)
    if (session.businessRole) localStorage.setItem(ROLE_KEY, session.businessRole)
    else localStorage.removeItem(ROLE_KEY)
  },

  clear(): void {
    localStorage.removeItem(ACCESS_KEY)
    localStorage.removeItem(REFRESH_KEY)
    localStorage.removeItem(BUSINESS_KEY)
    localStorage.removeItem(ROLE_KEY)
  },
}
