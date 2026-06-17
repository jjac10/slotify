# API Endpoints - Slotify

## Convenciones

- **Base URL:** `https://api.slotify.local:5000`
- **Auth:** Bearer token en header `Authorization: Bearer {JWT}`
- **Formato:** JSON
- **Errores:** HTTP status codes estándar + error detail
- **CORS:** habilitado para el frontend; orígenes permitidos en `Cors:AllowedOrigins` (dev: `http://localhost:5173`, `http://localhost:3000`)

---

## Autenticación

### POST /auth/register
Registrar un **cliente** (sin negocio; `type=customer`).

**Request:**
```json
{
  "email": "ana@example.com",
  "password": "SecurePass123!",
  "name": "Ana",
  "phone": "+34912345678"
}
```
> `phone` es **opcional**: si se indica, al registrarse se **vinculan automáticamente**
> las reservas previas hechas como invitado con ese teléfono/email (sync invitado→usuario).

**Response:** 201 — `businessId` es `null` (el cliente no tiene negocio).
```json
{ "userId": "uuid", "businessId": null, "accessToken": "jwt", "refreshToken": "opaque_token" }
```

> **Contraseña:** mín. 8 caracteres con mayúscula, minúscula, dígito y símbolo.
> Si no cumple → `400` (`weak_password`, con `details`). Email duplicado → `409`.
> Reservar NO exige registrarse (existe el flujo *guest*).

---

### POST /auth/register-owner
Registrar un **propietario** + su negocio (plan Free) + owner-as-staff, de forma atómica.

**Request:**
```json
{
  "email": "owner@example.com",
  "password": "SecurePass123!",
  "name": "Pepe",
  "businessName": "Mi Salón"
}
```

**Response:** 201 (incluye `businessId` del negocio creado).
```json
{ "userId": "uuid", "businessId": "uuid", "accessToken": "jwt", "refreshToken": "opaque_token" }
```
Mismas reglas de contraseña (`400`) y email duplicado (`409`) que el registro de cliente.

---

### POST /auth/login
Autenticar usuario.

**Request:**
```json
{
  "email": "owner@example.com",
  "password": "SecurePass123!"
}
```

**Response:** 200 — mismo `AuthResult` que el registro. Para un **owner**, `businessId`
es el de su negocio (igual que en `refresh`); para un cliente es `null`. Así el
frontend sabe si mostrar las secciones de propietario tras un login (no solo tras
el registro).
```json
{ "userId": "uuid", "businessId": "uuid", "accessToken": "jwt", "refreshToken": "opaque_token" }
```

---

### POST /auth/refresh
Renovar access token.

**Request:**
```json
{
  "refreshToken": "refresh_uuid"
}
```

**Response:** 200
```json
{
  "accessToken": "new_jwt",
  "expiresIn": 86400
}
```

---

### GET /auth/me
Datos del usuario autenticado.

**Auth:** Required (Bearer token)
**Response:** 200
```json
{
  "userId": "uuid",
  "email": "owner@example.com"
}
```
Sin token válido → 401.

---

## Negocios (Businesses)

### GET /businesses
Lista los negocios del owner autenticado.

**Auth:** Required (Bearer token)
**Response:** 200
```json
[ { "id": "uuid", "name": "Mi Salón", "status": "active" } ]
```

---

## Servicios (Services)

> Crear servicio es **solo owner** del negocio y respeta el límite del plan
> (Free=5): si se supera → `409` (`limit_reached`). Listar es público.

### GET /businesses/{businessId}/services
Listar servicios del negocio.

**Auth:** Optional (público si negocio es público)
**Response:** 200
```json
[
  {
    "id": "uuid",
    "name": "Corte de cabello",
    "duration_minutes": 30,
    "price": 25.00,
    "color": "#FF5733"
  }
]
```

---

### POST /businesses/{businessId}/services
Crear servicio.

**Auth:** Required (owner only)
**Request:**
```json
{
  "name": "Corte de cabello",
  "description": "Corte clásico",
  "durationMinutes": 30,
  "price": 25.00,
  "color": "#FF5733"
}
```

**Response:** 201

**Tests:**
- ✓ Owner puede crear
- ✓ Non-owner rechazado (403)
- ✓ Validar duración > 0
- ✓ Validar color hex
- ✓ Respetar límite free (5) vs premium

---

## Staff (Trabajadores)

### GET /businesses/{businessId}/staff
Lista los trabajadores **activos** de un negocio, ordenados por nombre. Público:
el cliente lo usa para elegir con quién reservar (el `staffId` que exige crear una
reserva). El owner aparece como staff (`role=owner`).

**Auth:** Optional (público)
**Response:** 200
```json
[
  {
    "id": "uuid",
    "businessId": "uuid",
    "name": "Pepe",
    "role": "owner",
    "status": "active"
  }
]
```
> No expone email/teléfono del trabajador. Los inactivos se excluyen.

---

## Disponibilidad

### GET /businesses/{businessId}/availability
Obtener slots disponibles para un servicio.

**Auth:** Optional
**Query params:**
```
?serviceId=uuid&date=2026-06-20&timezone=Europe/Madrid
```

**Response:** 200
```json
{
  "serviceId": "uuid",
  "date": "2026-06-20",
  "timezone": "Europe/Madrid",
  "availableSlots": [
    { "start": "09:00", "end": "09:30" },
    { "start": "09:30", "end": "10:00" },
    { "start": "10:00", "end": "10:30" }
  ]
}
```

**Tests:**
- ✓ Excluir horas cerradas
- ✓ Excluir slots ocupados
- ✓ Respetar zona horaria
- ✓ Excluir feriados
- ✓ Rendimiento: <100ms para 30 días

---

## Reservaciones

### POST /reservations
Crear reserva (guest o logged).

**Auth:** Optional
**Request:**
```json
{
  "businessId": "uuid",
  "serviceId": "uuid",
  "startTime": "2026-06-20T14:00:00Z",
  
  // Para invitado (guest=true)
  "guestName": "Juan Pérez",
  "guestPhone": "+34912345678",  // XOR guestEmail
  
  // Para usuario registrado
  "userId": "uuid"  // omitir si guest
}
```

**Response:** 201
```json
{
  "id": "uuid",
  "status": "pending",
  "confirmationToken": "abc123",
  "confirmationLink": "https://slotify.local/confirm/abc123"
}
```

**Tests (TDD - Crítico):**
- ✓ Guest: teléfono O email (no ambos, no ninguno)
- ✓ Guest: encriptar teléfono/email en BD
- ✓ Validar slot disponible
- ✓ **Doble booking prevention:** simulación concurrente
- ✓ Generar token confirmación único
- ✓ Enviar email/SMS confirmación
- ✓ Registrar en notification_logs

---

### GET /reservations/{id}
Obtener detalles de reserva (guest o owner).

**Auth:** Optional
**Query params:**
```
?confirmationToken=abc123  // para guest sin login
```

**Response:** 200
```json
{
  "id": "uuid",
  "serviceName": "Corte de cabello",
  "startTime": "2026-06-20T14:00:00Z",
  "endTime": "2026-06-20T14:30:00Z",
  "guestName": "Juan Pérez",
  "status": "confirmed",
  "notes": "Preferencia de largo"
}
```

---

### GET /reservations/mine
"Mis reservas": las del usuario autenticado (no canceladas), ordenadas por inicio.

**Auth:** Required (JWT)
**Response:** 200 — array de `ReservationResponse`.
```json
[ { "id": "uuid", "businessId": "uuid", "serviceId": "uuid", "staffId": "uuid",
    "userId": "uuid", "guestId": null,
    "startTime": "2026-09-05T12:00:00Z", "endTime": "2026-09-05T12:30:00Z", "status": "pending" } ]
```

**Tests:**
- ✓ Solo devuelve las reservas del propio usuario (no las de invitados)
- ✓ 401 sin token

---

### PATCH /reservations/{id}
Reprogramar reserva: cambia el inicio y **conserva la duración** (el fin se recalcula).

**Auth:** Required (JWT) — autoriza el owner del negocio, el staff del negocio o el propio usuario de la reserva.
**Request:**
```json
{
  "startTime": "2026-06-20T15:00:00Z"
}
```

**Response:** 200 — la reserva actualizada (`ReservationResponse`).

**Errores:**
- `401` sin token · `403` sin permiso sobre la reserva
- `404` la reserva no existe
- `409 slot_unavailable` el nuevo horario solapa con otra reserva del mismo staff (garantía dura: exclusion constraint en BD)
- `409 concurrency_conflict` otra operación modificó la reserva entretanto (optimistic locking por `version`)

**Notas:**
- Auditoría: registra `action='updated'` con el horario anterior (`old_values`) y el nuevo (`new_values`).
- El pre-check de solape se excluye a sí misma; la BD lo garantiza con el exclusion constraint (ADR #4).

**Tests:**
- ✓ Reprograma a hueco libre (200, conserva duración) + auditoría
- ✓ 401 sin token · 403 por otro owner · 409 sobre hueco ocupado
- ✓ Optimistic locking: version obsoleta → `ReservationConcurrencyException`
- 🔮 Notificar owner + guest (pendiente: notificaciones)

---

### DELETE /reservations/{id}
Cancelar reserva.

**Auth:** Optional + token
**Query params:**
```
?confirmationToken=abc123  // si guest
```

**Request:**
```json
{
  "reason": "No puedo asistir"
}
```

**Response:** 204

**Tests:**
- ✓ Validar >= cancellation_hours antes
- ✓ Notificar owner + guest
- ✓ Registrar motivo

---

## Dashboard Owner

### GET /businesses/{businessId}/dashboard
Resumen del negocio para su propietario.

**Auth:** Required (solo el owner del negocio).
**Response:** 200
```json
{
  "totalReservations": 45,
  "reservationsThisMonth": 12,
  "estimatedMonthlyRevenue": 1250.00,
  "upcomingReservations": [
    { "id": "uuid", "businessId": "uuid", "serviceId": "uuid", "staffId": "uuid",
      "userId": null, "guestId": "uuid",
      "startTime": "2026-06-20T10:00:00Z", "endTime": "2026-06-20T10:30:00Z", "status": "pending" }
  ]
}
```

**Métricas:**
- `totalReservations`: reservas no canceladas del negocio (histórico).
- `reservationsThisMonth`: reservas cuyo inicio cae en el mes en curso (UTC).
- `estimatedMonthlyRevenue`: suma del **precio del servicio** de las reservas del mes
  (los servicios gratuitos suman 0). Es una estimación: el precio puede cambiar con el tiempo.
- `upcomingReservations`: las próximas (inicio ≥ ahora), ordenadas por inicio, máx. 5
  (`ReservationResponse`).

**Errores:** `401` sin token · `403` (`forbidden`) si no es el owner · `404` (`business_not_found`) si el negocio no existe.

> Pendiente (🔮): `noShowRate` (requiere marcar asistencia) y `occupancyRate` (requiere
> aforo sobre el horario). Se omiten a propósito hasta que existan los datos que los sustenten.

**Tests:**
- ✓ Owner: 200 con contadores + próximas reservas
- ✓ 401 sin token · 403 por otro owner · 404 negocio inexistente
- ✓ Ingresos = suma del precio del servicio (gratuito = 0); contadores con ventana de mes

---

### GET /businesses/{businessId}/reservations
Agenda del negocio: reservas (no canceladas) ordenadas por inicio.

**Auth:** Required (owner del negocio o staff del negocio; otro usuario → 403)
**Query params (opcionales):**
```
?date=2026-09-01     // solo ese día (UTC)
&staffId=<uuid>      // solo ese trabajador
```

**Response:** 200 — array de `ReservationResponse`.
```json
[ { "id": "uuid", "businessId": "uuid", "serviceId": "uuid", "staffId": "uuid",
    "userId": null, "guestId": "uuid",
    "startTime": "2026-09-01T10:00:00Z", "endTime": "2026-09-01T10:30:00Z", "status": "pending" } ]
```

**Tests:**
- ✓ Owner ve las reservas del negocio; filtro por fecha
- ✓ 401 sin token · 403 si no es owner/staff del negocio
- 🔮 paginación + filtro por estado (pendiente)

---

## Error Responses

### 400 Bad Request
```json
{
  "error": "validation_error",
  "message": "El servicio requiere duración mayor a 15 minutos",
  "details": {
    "durationMinutes": ["Must be >= 15"]
  }
}
```

### 401 Unauthorized
```json
{
  "error": "unauthorized",
  "message": "Token inválido o expirado"
}
```

### 403 Forbidden
```json
{
  "error": "forbidden",
  "message": "No tienes permisos para esta acción"
}
```

### 404 Not Found
```json
{
  "error": "not_found",
  "message": "Reserva no encontrada"
}
```

### 409 Conflict
```json
{
  "error": "slot_unavailable",
  "message": "El slot ya no está disponible. Otra persona lo reservó.",
  "availableSlots": [...]
}
```

### 429 Too Many Requests
```json
{
  "error": "rate_limit_exceeded",
  "message": "Demasiadas solicitudes. Intenta en 60 segundos"
}
```

### 500 Internal Server Error
```json
{
  "error": "internal_error",
  "message": "Error interno. Contacta al soporte",
  "traceId": "uuid"
}
```

---

## Rate Limiting

- **Límite:** 100 req/min por IP
- **Header:** `X-RateLimit-Remaining`, `X-RateLimit-Reset`
- **Exceción:** GET sin cambios de estado (1000 req/min)

---

## Paginación

```json
{
  "total": 500,
  "page": 1,
  "pageSize": 20,
  "totalPages": 25,
  "data": [...]
}
```

---

## Futures (Fase 4+)

- [ ] Webhook para integraciones
- [ ] GraphQL endpoint
- [ ] Batch operations
- [ ] Server-Sent Events para notificaciones en tiempo real
