# API Endpoints - Slotify

## Convenciones

- **Base URL:** `https://api.slotify.local:5000`
- **Auth:** Bearer token en header `Authorization: Bearer {JWT}`
- **Formato:** JSON
- **Errores:** HTTP status codes estándar + error detail

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

**Response:** 200
```json
{
  "accessToken": "jwt",
  "refreshToken": "refresh_uuid",
  "expiresIn": 86400,
  "user": { "id": "uuid", "email": "...", "plan": "free" }
}
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
Resumen para propietario.

**Auth:** Required (owner)
**Response:** 200
```json
{
  "totalReservations": 45,
  "monthlyRevenue": 1250.00,
  "upcomingReservations": [
    { "id": "...", "guestName": "...", "time": "...", "status": "pending" }
  ],
  "noShowRate": 0.05,
  "occupancyRate": 0.75
}
```

---

### GET /businesses/{businessId}/reservations
Listar reservas del negocio.

**Auth:** Required (owner)
**Query params:**
```
?status=pending&from=2026-06-01&to=2026-06-30&page=1&limit=20
```

**Response:** 200
```json
{
  "total": 120,
  "page": 1,
  "data": [...]
}
```

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
