# API Endpoints - Slotify

## Convenciones

- **Base URL:** `https://api.slotify.local:5000`
- **Auth:** Bearer token en header `Authorization: Bearer {JWT}`
- **Formato:** JSON
- **Errores:** HTTP status codes estándar + error detail

---

## Autenticación

### POST /auth/register
Registrar nuevo propietario de negocio.

**Request:**
```json
{
  "email": "owner@example.com",
  "password": "SecurePass123!",
  "businessName": "Mi Salón"
}
```

**Response:** 201
```json
{
  "userId": "uuid",
  "businessId": "uuid",
  "accessToken": "jwt",
  "refreshToken": "refresh_uuid",
  "expiresIn": 86400
}
```

**Tests (TDD):**
- ✓ Email inválido rechazado
- ✓ Contraseña corta rechazada
- ✓ Email duplicado rechazado
- ✓ Negocio creado automáticamente en Free plan

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

## Servicios (Services)

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
Modificar reserva (cambiar hora).

**Auth:** Optional + token
**Request:**
```json
{
  "newStartTime": "2026-06-20T15:00:00Z",
  "confirmationToken": "abc123"  // si guest
}
```

**Response:** 200

**Tests:**
- ✓ Validar nuevo slot disponible
- ✓ Notificar owner + guest
- ✓ Validar permissions (guest token o user logged)

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
