import { useEffect, useRef } from 'react'
import { HubConnectionBuilder } from '@microsoft/signalr'
import { tokenStorage } from '../services/tokenStorage'
import type { ReservationChangedEvent } from '../types/api'

/**
 * Suscripción en tiempo real a /hubs/reservations (SignalR, vía el proxy /api):
 * el backend emite "reservationChanged" al canal del usuario autenticado y, si se
 * pasa `businessId` (owner/staff), también al grupo del negocio. Best-effort: si el
 * hub no conecta, la app funciona igual (sin refresco automático).
 */
export function useReservationEvents(
  onEvent: (e: ReservationChangedEvent) => void,
  businessId?: string | null,
) {
  // El handler vive en un ref: cambiarlo no debe reconectar el hub.
  const handlerRef = useRef(onEvent)
  handlerRef.current = onEvent

  useEffect(() => {
    if (!tokenStorage.getAccessToken()) return // sin sesión no hay canal

    const connection = new HubConnectionBuilder()
      .withUrl('/api/hubs/reservations', {
        accessTokenFactory: () => tokenStorage.getAccessToken() ?? '',
      })
      .withAutomaticReconnect()
      .build()

    connection.on('reservationChanged', (e: ReservationChangedEvent) => handlerRef.current(e))

    const joinBusiness = () => {
      if (businessId) connection.invoke('JoinBusiness', businessId).catch(() => { /* sin grupo */ })
    }
    connection.onreconnected(joinBusiness)

    let active = true
    connection
      .start()
      .then(() => { if (active) joinBusiness() })
      .catch(() => { /* sin tiempo real; la app sigue funcionando */ })

    return () => {
      active = false
      connection.stop().catch(() => { /* ya cerrada */ })
    }
  }, [businessId])
}
