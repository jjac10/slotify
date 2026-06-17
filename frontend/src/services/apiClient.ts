import axios from 'axios'
import type { AxiosError, InternalAxiosRequestConfig } from 'axios'
import { tokenStorage } from './tokenStorage'
import type { ApiError } from '../types/api'

/**
 * Cliente HTTP único de la app. baseURL = `/api` (ruta relativa): en dev el
 * proxy de Vite la reenvía al backend y en prod lo hace nginx, así que el
 * navegador siempre habla con su mismo origen (sin CORS).
 */
export const api = axios.create({
  baseURL: '/api',
  headers: { 'Content-Type': 'application/json' },
})

// Handler que la capa de auth registra para reaccionar a un 401 (sesión expirada).
let onUnauthorized: (() => void) | null = null

export function registerUnauthorizedHandler(handler: (() => void) | null): void {
  onUnauthorized = handler
}

// Request: adjunta el Bearer token si hay sesión.
api.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const token = tokenStorage.getAccessToken()
  if (token) {
    config.headers.set('Authorization', `Bearer ${token}`)
  }
  return config
})

// Response: un 401 en una petición autenticada ⇒ limpiar sesión y avisar.
// (No se dispara en un login fallido, porque entonces aún no hay token guardado.)
api.interceptors.response.use(
  (response) => response,
  (error: AxiosError<ApiError>) => {
    if (error.response?.status === 401 && tokenStorage.getAccessToken()) {
      tokenStorage.clear()
      onUnauthorized?.()
    }
    return Promise.reject(error)
  },
)

/** Extrae el cuerpo de error tipado del backend, o null si no es un error de la API. */
export function getApiError(error: unknown): ApiError | null {
  if (axios.isAxiosError(error)) {
    return (error as AxiosError<ApiError>).response?.data ?? null
  }
  return null
}
