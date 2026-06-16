# Diseño — Núcleo de reservas

> Diseño de la rama `feature/reservations-core`. Esquema canónico: [`../DATA_MODEL.md`](../DATA_MODEL.md).
> Decisiones: [`../DECISIONS.md`](../DECISIONS.md). Estado/plan: [`../ROADMAP.md`](../ROADMAP.md).

## 1. Alcance

**Dentro de esta rama:**
- Entidad/tabla `guests` con cifrado **AES-256-GCM** (recuperable) + **HMAC-SHA256** (blind index).
- Entidad/tabla `reservations` con anti-doble-booking **robusto** + optimistic locking (`version`).
- `CryptoService` (cifrado) + blind index (HMAC) para teléfono/email de invitado.
- `BookingService.CreateAsync` (reserva como invitado o usuario logueado) con validación de solapamiento.
- Endpoints `POST /reservations` y `GET /reservations/{id}`.

**Fuera (ramas posteriores):** modificar/cancelar reserva (con permisos por rol), `confirmation_tokens`
(acceso guest por link), `notification_logs`, `audit_logs`, `waitlists`, `reviews`,
disponibilidad por `business_hours`/slots.

## 2. Entidades

### `guests`
- `id`, `business_id` (FK→businesses, NOT NULL, CASCADE), `name` (NOT NULL).
- `phone_encrypted` / `email_encrypted`: AES-256-GCM (base64 `IV(12)+Tag(16)+Ciphertext`).
- `phone_hash` / `email_hash`: HMAC-SHA256 hex del valor **normalizado** (tel→E.164, email→lower+trim).
- `user_id` (FK→users, NULL hasta que el guest se registra).
- `total_reservations`, `last_reservation_at`, `status`, `created_at`, `updated_at`.
- **CHECK** `phone_hash IS NOT NULL OR email_hash IS NOT NULL`.
- **UNIQUE parcial** `(business_id, phone_hash)` y `(business_id, email_hash)` → dedupe por negocio.

### `reservations`
- `id`, `business_id`, `service_id` (RESTRICT), `staff_id` (RESTRICT) — todos NOT NULL.
- `user_id` XOR `guest_id` (**CHECK** `user_or_guest`).
- `start_time`, `end_time` (timestamptz, UTC; **CHECK** `start < end`).
- `status` (pending/confirmed/cancelled/no-show), `payment_status` ('not_required').
- `version` (int) → **optimistic locking** (concurrency token EF).
- `created_at`, `updated_at`, `cancelled_at`.
- Índices: `(business_id, start_time)`, `(staff_id, start_time)`, `(guest_id, start_time)`, `(status, created_at)`.

## 3. Cifrado de invitado (ADR #5)

Dos claves distintas (config/.env), nunca la misma:
- **`ICryptoService`** (AES-256-GCM): `Encrypt`/`Decrypt`. Recuperable → enviar confirmación.
- **Blind index** (HMAC-SHA256 determinista) sobre valor normalizado → permite **buscar** y **UNIQUE**
  (AES-GCM cambia cada vez y no es indexable).

## 4. Anti-doble-booking — opción B (robusta, ADR #4)

Defensa en dos capas + garantía a nivel BD:
1. **Servicio:** antes de insertar, comprobar que no exista reserva del mismo `staff_id` con
   `existing.start < new.end AND existing.end > new.start` (status ≠ cancelled).
2. **PostgreSQL — exclusion constraint** (extensión `btree_gist`):
   `EXCLUDE USING gist (staff_id WITH =, tstzrange(start_time, end_time) WITH &&) WHERE (status <> 'cancelled')`.
   Garantiza a nivel BD que **ningún** solapamiento (exacto o parcial) del mismo trabajador puede persistir,
   incluso bajo concurrencia. Una violación → `DbUpdateException` → `409`.

## 5. Flujo `BookingService.CreateAsync`

```
Entrada: businessId, serviceId, staffId, startTime + (guestName + phone XOR email) | userId (del JWT)
1. Cargar service → endTime = startTime + duration_minutes.
2. Validar staff pertenece al negocio (y, a futuro, que atiende ese service vía staff_services).
3. Cliente: si logueado → user_id; si guest → normalizar, calcular *_hash, buscar/crear guest (encrypted + hash).
4. Validar solapamiento (capa servicio).
5. Insertar reservation (status='pending', version=0). Violación de exclusion constraint → SlotUnavailableException (409).
6. Devolver ReservationResponse.
```

## 6. Endpoints

- `POST /reservations` — `[AllowAnonymous]` (guest o logueado; si hay JWT, `user_id` del token). `201` · `409` · `400`.
- `GET /reservations/{id}` — datos de la reserva (el acceso guest por `confirmationToken` llega en otra rama).

## 7. Sub-ciclos TDD

1. `CryptoService` (AES-GCM round-trip) + HMAC blind index — unit.
2. `guests` + repo (crear, buscar por hash, dedupe UNIQUE) — integración.
3. `reservations` + exclusion constraint anti-doble-booking + optimistic `version` — integración.
4. `BookingService.CreateAsync` (guest y user, endTime, dedupe, solapamiento) — unit (Moq).
5. Solapamiento real + colisión concurrente → 409 — integración.
6. Endpoints `POST /reservations` + `GET /reservations/{id}` — integración (API real).

---

## Anexo A — Slots / disponibilidad (rama futura)

Los slots **no se almacenan**: se calculan = `horario_negocio − festivos − descansos − reservas`.
- **Granularidad configurable** `slot_interval_minutes` (por negocio; default = duración del servicio
  más corto, o GCD de las duraciones).
- **Regla anti-huecos:** un inicio `S` en un bloque libre `[a,b)` solo se ofrece si el hueco previo
  `(S−a)` es `0` o `≥` duración del servicio más corto (no dejar minutos muertos no reservables).
- **Empaquetado:** anclar candidatos al inicio del bloque y al final de cada reserva existente (back-to-back).
- Extras previstos: `buffer_minutes` por servicio, antelación mínima, horizonte de reserva, horario por staff,
  capacidad/paralelo (`max_slots_per_service`).

## Anexo B — Roles y permisos (rama futura)

Autorización **por negocio** vía la tabla `staff` (no por `users.type`): un mismo user puede ser owner de
un negocio, employee de otro y customer en otro. `IBusinessAccess.GetRole(userId, businessId)` → `owner|employee|none`.

| Acción | Owner | Employee | Customer | Guest |
|---|---|---|---|---|
| Gestionar negocio / servicios / staff | ✅ | ❌ | ❌ | ❌ |
| Ver todas las reservas del negocio | ✅ | (configurable) | ❌ | ❌ |
| Crear/mover/cancelar reservas del negocio | ✅ | ✅ (suyas; todas si owner lo permite) | ❌ | ❌ |
| Gestionar su propia reserva | — | — | ✅ | ✅ (por token) |

## Anexo C — Rol admin de plataforma (futuro)

Admin (operador de la plataforma) **por encima de los negocios** → único caso de tipo global:
`users.type = 'admin'`, endpoints `/admin/*`. Gestiona owners/negocios. Opcional a futuro: cerrar el
registro abierto para que solo admin provisione owners. No se implementa ahora.
