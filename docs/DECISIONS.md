# Architecture Decision Records (ADRs) - Slotify

## 1. Code First Approach (EF Core Migrations)

**Status:** ✅ Accepted
**Context:** Necesitamos versionable schema + facilidad para cambiar BD

**Decision:** Usar EF Core Code First + Migrations
- C# como source of truth
- Migrations en git (auditable)
- Fácil rollback
- Soporta multiple DB engines

**Consequences:**
- ✅ Schema visible en código
- ✅ Migraciones reversibles
- ❌ Cambios manuales a BD no se syncan automáticamente

**Alternativas rechazadas:**
- Database First: Schema desacoplada del código
- Fluent API manual: Más boilerplate

---

## 2. Repository Pattern + DI

**Status:** ✅ Accepted
**Context:** Queremos BD intercambiable sin tocar services

**Decision:** Repository Pattern + Dependency Injection
```csharp
// Interface
public interface IReservationRepository
{
    Task<bool> IsSlotAvailableAsync(...);
    Task AddAsync(Reservation r);
}

// Service recibe interface (no DbContext directo)
public class ReservationService
{
    public ReservationService(IReservationRepository repo) { }
}

// Diferentes implementaciones
public class PostgreSqlReservationRepository : IReservationRepository { }
public class MongoReservationRepository : IReservationRepository { }
```

**Consequences:**
- ✅ Cambiar PostgreSQL → MongoDB = 1 nueva clase
- ✅ Testeable con mocks
- ❌ Abstracción extra (interface duplication)

---

## 3. JWT Authentication (no Sessions)

**Status:** ✅ Accepted
**Context:** API stateless, preparado para escalar horizontalmente

**Decision:** JWT tokens
- Access token: 24h
- Refresh token: 7d
- HS256 signing
- No estado en servidor

**Structure:**
```json
{
  "sub": "user_id",
  "email": "owner@example.com",
  "plan": "free",
  "exp": 1719000000,
  "iat": 1718913600
}
```

**Consequences:**
- ✅ Escalable (sin session store)
- ✅ CORS-friendly
- ❌ No invalidación inmediata (revocation list needed si logout urgente)

**Alternativas rechazadas:**
- Sessions: Requiere estado compartido en cluster
- OAuth2: Overkill para MVP

---

## 4. Optimistic Locking para Doble Booking

**Status:** ✅ Accepted
**Context:** Prevenir race conditions en slots (crítico)

**Decision:** Versioning + Unique constraint
```sql
-- Unique constraint POR TRABAJADOR (no por service): dos trabajadores pueden dar
-- el mismo servicio a la vez, así que la unicidad va sobre (staff_id, start_time).
-- Si fuera por service bloquearía reservas legítimas. Schema canónico: DATA_MODEL.md.
CREATE UNIQUE INDEX idx_no_double_booking
ON reservations(staff_id, start_time)
WHERE status != 'cancelled';

-- Entity versioning
public class Reservation {
    public int Version { get; set; } // Incrementa en cada UPDATE
}
```

> Esto evita solapamientos exactos de inicio. Los solapamientos parciales
> (una cita de 60 min que pisa otra del mismo `staff_id`) se validan en la capa
> de servicio comprobando rangos `[start_time, end_time)`.

**Flow:**
1. READ reservation (versión 0)
2. MODIFY en código
3. UPDATE + WHERE version = 0
4. Si otro lo actualizó, falla UPDATE (versión ya 1)
5. Retry o error 409 al cliente

**Consequences:**
- ✅ No locks de lectura
- ✅ Rendimiento alto
- ❌ Client debe manejar retry
- ❌ Timestamp collision en PostgreSQL (mitigado con UUID ordenado)

**Alternativas rechazadas:**
- Pessimistic locking (SELECT FOR UPDATE): Degrade performance
- Event sourcing: Complejidad excesiva para MVP

---

## 5. Encryption at Rest para Datos Sensibles

**Status:** ✅ Accepted
**Context:** GDPR + seguridad de invitados

**Decision:** AES-256-GCM por columna
- `guest_phone_encrypted`
- `guest_email_encrypted`
- Master key en .env (rotación anual)

**Implementation:**
```csharp
public class CryptoService
{
    public string Encrypt(string plaintext)
    {
        var iv = RandomNumberGenerator.GetBytes(16);
        var cipher = new AesGcm(masterKey);
        var tag = new byte[16];
        var ciphertext = new byte[plaintext.Length];
        
        cipher.Encrypt(iv, Encoding.UTF8.GetBytes(plaintext), null, ciphertext, tag);
        
        // Return: base64(IV + Tag + Ciphertext)
        return Convert.ToBase64String(iv.Concat(tag).Concat(ciphertext).ToArray());
    }
}
```

**Consequences:**
- ✅ Cumple GDPR
- ✅ Búsqueda por plaintext requiere decrypt (lento pero seguro)
- ❌ Key rotation compleja
- ❌ Performance (decrypt en cada query)

**Alternativas rechazadas:**
- Plain storage: GDPR violation risk
- Hashing (one-way): No se puede recuperar para enviar confirmación

---

## 6. Async Notifications (Fire & Forget)

**Status:** ✅ Accepted
**Context:** Notificaciones no deben bloquear response HTTP

**Decision:** Async publish + Background job
```csharp
// En controller/service
var result = await _reservationService.CreateAsync(request);

// Fire & forget (no await)
_ = _notificationService.SendAsync(result.Id);

return CreatedAtAction(..., result);
```

**Consequences:**
- ✅ Response rápida al cliente
- ✅ Escalable
- ❌ Posible pérdida si servicio cae (mitigado con retries)
- ❌ Eventual consistency

**Alternativas rechazadas:**
- Sync notifications: Bloquea response si email lento
- Message queue (Rabbit, Kafka): Overkill para MVP

---

## 7. Timezone Strategy (UTC + Local Display)

**Status:** ✅ Accepted
**Context:** Negocio en Madrid, clientes en múltiples zonas

**Decision:**
- BD: Todo en UTC
- Request/Response: Include timezone
- Conversión en Frontend

**Example:**
```json
// Request
{
  "serviceId": "uuid",
  "startTime": "2026-06-20T14:00:00Z",  // UTC
  "timezone": "Europe/Madrid"
}

// Response
{
  "startTime": "2026-06-20T14:00:00Z",  // UTC
  "displayTime": "2026-06-20T16:00:00"  // Local para UI
}
```

**Consequences:**
- ✅ No ambigüedad histórica (DST transitions)
- ✅ Fácil cambiar zona negocio
- ❌ Manual conversion cada query
- ❌ Riesgos si frontend olvida convertir

---

## 8. CQRS-Lite para Reports (Futuro)

**Status:** 🔮 Future (Fase 4)
**Context:** Analytics requieren lecturas diferentes a operacionales

**Decision:** Preparar con materialized views + read replicas
- No implementar en MVP
- Schema permite agregar read DB
- Repository pattern abstrae source

**Implementation approach:**
```sql
-- Futuro: materialized view para reportes
CREATE MATERIALIZED VIEW reports.monthly_reservations AS
SELECT 
  DATE_TRUNC('month', start_time) as month,
  COUNT(*) as total,
  SUM(CASE WHEN status = 'cancelled' THEN 1 ELSE 0 END) as cancellations
FROM reservations
GROUP BY DATE_TRUNC('month', start_time);
```

---

## 9. Freemium Limits (Data-Driven)

**Status:** ✅ Accepted
**Context:** Monetización flexible + fácil agregar tiers

**Decision:** Feature flags + DB parametrización
```sql
-- pricing_tiers table (schema canónico completo en DATA_MODEL.md)
CREATE TABLE pricing_tiers (
  id UUID PRIMARY KEY,
  code VARCHAR(50) UNIQUE NOT NULL, -- "free", "premium" (clave estable usada en código)
  name VARCHAR(100) NOT NULL,       -- nombre visible
  max_reservations_per_month INT,   -- NULL = ilimitado
  max_services INT,
  max_staff INT,
  has_api BOOLEAN,
  ...
);

-- businesses.tier_id (NOT NULL) referencia pricing_tiers.
-- El plan vive en el NEGOCIO, no en el user: un owner puede tener varios negocios.
```

**Validation en Service:**
```csharp
public async Task<bool> CanCreateServiceAsync(Guid businessId)
{
    var tier = await _tierRepository.GetByBusinessAsync(businessId);
    var count = await _serviceRepository.CountByBusinessAsync(businessId);
    
    return count < tier.MaxServices;
}
```

**Consequences:**
- ✅ Agregar tier = solo insertar row
- ✅ Sin hardcoding en código
- ❌ Validación distribuida (cada query)
- ❌ Caché de tiers necesario

---

## 10. Docker Compose para Dev (No Kubernetes)

**Status:** ✅ Accepted
**Context:** MVP en VPS Ionos, no necesita orquestación

**Decision:** Docker Compose local + Docker en producción
- Dev: docker-compose up
- Prod: docker pull + docker run en VPS

**No Kubernetes porque:**
- MVP pequeño
- 1 VPS Ionos no es cluster
- Overhead innecesario
- Mantenimiento más complejo

**Futures:**
- Si >3 servicios, considerar Docker Swarm
- Si >100k users, considerar K8s

---

## 11. Vite (no Webpack)

**Status:** ✅ Accepted
**Context:** Frontend moderno + fast refresh

**Decision:** Vite 5 + esbuild
- Dev server sub-segundo hot reload
- Build ~50ms
- Mejor DX

**Consequences:**
- ✅ Desarrollo rápido
- ✅ Pequeño bundle
- ❌ Requiere Node 16+
- ❌ Menos plugins que Webpack (pero suficiente)

---

## 12. TypeScript (Strict Mode)

**Status:** ✅ Accepted
**Context:** Prevenir runtime errors en frontend

**Decision:** TypeScript 5.x + strict: true
```json
{
  "compilerOptions": {
    "strict": true,
    "noImplicitAny": true,
    "strictNullChecks": true
  }
}
```

**Consequences:**
- ✅ Errores detectados en build
- ✅ Better IDE support
- ❌ Setup más verboso
- ❌ Curva aprendizaje

---

## 13. Hard Delete para Cancelaciones

**Status:** ✅ Accepted
**Context:** Canceladas no tienen valor histórico para negocio

**Decision:** DELETE registro, no soft delete (status = 'cancelled')
- Audit log registra acción antes de DELETE
- NotificationLog muestra evidencia de cancelación
- GDPR compliance: invitado puede solicitar borrado

**Implementation:**
```csharp
public async Task CancelAsync(Guid reservationId, string reason)
{
    var reservation = await _context.Reservations.FindAsync(reservationId);
    
    // Log antes de borrar
    await _auditService.LogAsync(reservationId, "deleted", reason);
    
    _context.Reservations.Remove(reservation);
    await _context.SaveChangesAsync();
}
```

**Consequences:**
- ✅ Datos limpios
- ✅ Privacidad (GDPR)
- ❌ Sin historial de cancellations (pero audit_logs lo registra)

---

## 14. Audit Logging para Reservas

**Status:** ✅ Accepted
**Context:** TFM requiere demostrar profesionalismo + compliance

**Decision:** Tabla `audit_logs` con JSONB snapshots
```sql
audit_logs (
  reservation_id, action, actor_id, old_values, new_values, ip_address, user_agent
)
```

**Captured Actions:**
- created (guest/user crea)
- updated (modifica hora)
- deleted (cancela)
- confirmed (owner confirma)

**Query ejemplo:**
```sql
-- ¿Qué pasó con la reserva X?
SELECT action, actor_type, old_values, new_values, created_at
FROM audit_logs
WHERE reservation_id = 'uuid'
ORDER BY created_at DESC;
```

**Consequences:**
- ✅ Trazabilidad completa
- ✅ Compliance (GDPR, auditoría)
- ✅ Debug + troubleshooting
- ❌ Storage extra (~1KB por operación)

---

## 15. Sincronización Invitado → Usuario

**Status:** ✅ Accepted
**Context:** Invitado reserva sin cuenta, luego quiere registrarse

**Decision:** Sincronización **automática en el registro** (sin endpoint manual).
- Invitado crea reserva → se crea un `guest` con `phone_hash`/`email_hash` (HMAC,
  blind index) y `phone_encrypted`/`email_encrypted` (AES-GCM, recuperable).
- Al registrarse con el mismo teléfono/email, el backend enlaza los guests
  poniendo `guests.user_id`. El historial se ve por `user_id` (directo) o por los
  `guest_id` ya vinculados al user. La reserva NUNCA cambia su `guest_id` a un
  user (el CHECK `user_or_guest` exige exactamente uno de los dos).

**Flow:**
```
1. POST /reservations (guest, phone="+34...")
   → guest.phone_hash = HMAC("+34..."), guest.phone_encrypted = AES("+34...")
   → reservation.guest_id = guest.id, reservation.user_id = NULL

2. POST /auth/register (email, phone="+34...")
   → user.id = "user-123"
   → UPDATE guests SET user_id = "user-123"
     WHERE phone_hash = HMAC("+34...") OR email_hash = HMAC(email)

3. GET /my-reservations
   → WHERE user_id = "user-123"
        OR guest_id IN (SELECT id FROM guests WHERE user_id = "user-123")
```

**Consequences:**
- ✅ No pierden historial; UX seamless (sin paso extra)
- ✅ Búsqueda/unicidad por `*_hash` (HMAC determinista), no por AES (rápido)
- ❌ Requiere normalizar antes del HMAC (teléfono E.164, email lowercase+trim)

---

## Decisiones Rechazadas y Por Qué

| Opción | Rechazada | Razón |
|--------|-----------|-------|
| Soft delete cancelaciones | ✗ | Hard delete limpia BD, audit_logs registra |
| Sin auditoría | ✗ | TFM + compliance lo requieren |
| Email-only para invitados | ✗ | Teléfono default (usuários prefieren SMS) |
| GraphQL | ✗ | REST suficiente, GraphQL overkill MVP |
| Stripe integración | ✗ | Futuro (Fase 4+), architecture preparada |
| Real-time WebSocket | ✗ | Server-sent events suficiente, polling para MVP |
| Microservicios | ✗ | Monolito modular mejor para MVP |
| Redis cache | ✗ | Futuro (cuando performance lo necesite) |
| SQL Server | ✗ | PostgreSQL open-source, mejor para VPS |

---

## Revisión Anual

Este documento se revisará:
- [ ] Trim 1 2027 (Fase 4 en prod)
- [ ] Trim 1 2028 (Post-MVP, 6+ meses live)

Se puede abrir issue con etiqueta `adr` para proponer cambios.
