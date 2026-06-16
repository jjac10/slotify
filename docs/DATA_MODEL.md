# Data Model - Slotify

## Entity Diagram

```
User (Owner)
  ├── Business (1:*)
  │   ├── Service (1:*)
  │   │   └── Reservation (1:*)
  │   ├── BusinessHour (1:*)
  │   ├── Staff (1:*)
  │   └── Notification (1:*)
  └── RefreshToken (1:*)

Reservation
  ├── Service
  ├── Guest (optional, si no logged)
  └── NotificationLog
```

---

## Tabla: `users`

```sql
CREATE TABLE users (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  email VARCHAR(255) UNIQUE NOT NULL,
  password_hash VARCHAR(255) NOT NULL,
  name VARCHAR(255) NOT NULL,
  phone VARCHAR(20),
  
  -- Type
  type VARCHAR(50) DEFAULT 'customer', -- customer (solo reservas), owner (tiene negocio)
  
  -- Plan (solo relevante si owner)
  plan VARCHAR(50) DEFAULT 'free', -- free, premium
  
  status VARCHAR(50) DEFAULT 'active', -- active, inactive, deleted
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  deleted_at TIMESTAMP NULL
);
```

**Índices:**
- `email` UNIQUE
- `type, status` (para queries)
- `created_at`

**Diferenciación:**
- `type = 'owner'` → Tiene ≥1 negocio (check en tabla businesses)
- `type = 'customer'` → Solo hace reservas, sin negocio propio

---

## Tabla: `businesses`

```sql
CREATE TABLE businesses (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  owner_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  
  -- Info Básica
  name VARCHAR(255) NOT NULL,
  description TEXT,
  category VARCHAR(100), -- barbershop, salon, gym, clinic, etc.
  
  -- Contacto
  phone VARCHAR(20),
  email VARCHAR(255),
  website VARCHAR(255),
  
  -- Ubicación
  address TEXT,
  city VARCHAR(100),
  postal_code VARCHAR(10),
  latitude DECIMAL(10,8),
  longitude DECIMAL(11,8),
  timezone VARCHAR(50) DEFAULT 'UTC',
  
  -- Customización Visual
  logo_url TEXT,
  banner_url TEXT,
  primary_color VARCHAR(7), -- #RRGGBB
  secondary_color VARCHAR(7),
  accent_color VARCHAR(7),
  
  -- Información Adicional
  about TEXT, -- Descripción larga del negocio
  business_hours_description TEXT, -- Ej: "Lunes-Viernes 9-18h, Sábado 10-14h"
  terms_and_conditions TEXT,
  cancellation_policy TEXT,
  
  -- Config Operacional
  max_slots_per_service INT DEFAULT 10,
  cancellation_hours INT DEFAULT 24, -- min horas antes de cancelar
  allow_guests BOOLEAN DEFAULT true, -- permitir reservas sin registro
  require_phone BOOLEAN DEFAULT true, -- teléfono obligatorio para guests
  
  -- Social
  instagram VARCHAR(255),
  facebook VARCHAR(255),
  
  -- Stats
  total_reservations INT DEFAULT 0,
  average_rating DECIMAL(3,2),
  
  status VARCHAR(50) DEFAULT 'active', -- active, inactive, deleted
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  
  CONSTRAINT fk_businesses_owner FOREIGN KEY (owner_id) REFERENCES users(id)
);
```

**Índices:**
- `owner_id`
- `name`
- `category`
- `city, status` (para búsqueda geolocalizada)
- `status, created_at`

---

## Tabla: `services`

```sql
CREATE TABLE services (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  business_id UUID NOT NULL REFERENCES businesses(id) ON DELETE CASCADE,
  name VARCHAR(255) NOT NULL,
  description TEXT,
  duration_minutes INT NOT NULL, -- 30, 60, 90, etc.
  price DECIMAL(10, 2), -- NULL = free service
  color VARCHAR(7), -- para calendario
  
  status VARCHAR(50) DEFAULT 'active', -- active, archived
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  
  CONSTRAINT fk_services_business FOREIGN KEY (business_id) REFERENCES businesses(id)
);
```

**Índices:**
- `business_id, status`
- `created_at`

---

## Tabla: `guests` (Clientes sin registrar)

```sql
CREATE TABLE guests (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  business_id UUID NOT NULL REFERENCES businesses(id) ON DELETE CASCADE,
  
  name VARCHAR(255) NOT NULL,
  phone_encrypted VARCHAR(255), -- XOR phone_encrypted OR email_encrypted
  email_encrypted VARCHAR(255),
  
  -- Unificación con user registrado
  user_id UUID UNIQUE REFERENCES users(id) ON DELETE SET NULL, -- NULL mientras sea guest
  
  -- Stats
  total_reservations INT DEFAULT 0,
  last_reservation_at TIMESTAMP,
  
  status VARCHAR(50) DEFAULT 'active', -- active, blocked
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  
  CONSTRAINT fk_guests_business FOREIGN KEY (business_id) REFERENCES businesses(id),
  CONSTRAINT phone_or_email CHECK (phone_encrypted IS NOT NULL OR email_encrypted IS NOT NULL)
);
```

**Índices:**
- `business_id, phone_encrypted` UNIQUE (cuando phone es identificador)
- `business_id, email_encrypted` UNIQUE (cuando email es identificador)
- `user_id` UNIQUE (linking a user registrado)

---

## Tabla: `reservations`

```sql
CREATE TABLE reservations (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  business_id UUID NOT NULL REFERENCES businesses(id) ON DELETE CASCADE,
  service_id UUID NOT NULL REFERENCES services(id) ON DELETE RESTRICT,
  
  -- Guest info (puede ser registered user O guest sin registrar)
  user_id UUID REFERENCES users(id) ON DELETE SET NULL, -- NULL si es guest sin registrar
  guest_id UUID REFERENCES guests(id) ON DELETE SET NULL, -- NULL si es user registrado
  
  -- Slot info
  start_time TIMESTAMP NOT NULL, -- Always UTC in DB
  end_time TIMESTAMP NOT NULL,
  
  -- Status workflow
  status VARCHAR(50) DEFAULT 'pending', -- pending, confirmed, cancelled, no-show
  notes TEXT,
  
  -- Concurrency control
  version INT DEFAULT 0, -- optimistic locking
  
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  cancelled_at TIMESTAMP NULL,
  
  CONSTRAINT fk_reservations_business FOREIGN KEY (business_id) REFERENCES businesses(id),
  CONSTRAINT fk_reservations_service FOREIGN KEY (service_id) REFERENCES services(id),
  CONSTRAINT fk_reservations_user FOREIGN KEY (user_id) REFERENCES users(id),
  CONSTRAINT fk_reservations_guest FOREIGN KEY (guest_id) REFERENCES guests(id),
  CONSTRAINT user_or_guest CHECK ((user_id IS NOT NULL AND guest_id IS NULL) OR (user_id IS NULL AND guest_id IS NOT NULL)),
  CONSTRAINT check_times CHECK (start_time < end_time)
);
```

**Índices:**
- `business_id, start_time` (crítico para availability check)
- `guest_id, start_time` (para historial)
- `status, created_at` (para reportes)
- `start_time` (para queries de "slots disponibles")

**Unique Constraint:**
```sql
CREATE UNIQUE INDEX idx_no_double_booking 
ON reservations(service_id, start_time, status) 
WHERE status != 'cancelled';
```

---

## Tabla: `business_hours`

```sql
CREATE TABLE business_hours (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  business_id UUID NOT NULL REFERENCES businesses(id) ON DELETE CASCADE,
  day_of_week INT NOT NULL, -- 0=Sunday, 1=Monday, ..., 6=Saturday
  
  is_closed BOOLEAN DEFAULT false,
  opening_time TIME, -- NULL si is_closed = true
  closing_time TIME,
  
  CONSTRAINT fk_business_hours_business FOREIGN KEY (business_id) REFERENCES businesses(id),
  CONSTRAINT check_times CHECK (opening_time < closing_time OR is_closed = true)
);
```

**Índices:**
- `business_id, day_of_week` UNIQUE

---

## Tabla: `business_holidays`

```sql
CREATE TABLE business_holidays (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  business_id UUID NOT NULL REFERENCES businesses(id) ON DELETE CASCADE,
  holiday_date DATE NOT NULL,
  reason VARCHAR(255),
  is_closed BOOLEAN DEFAULT true,
  
  CONSTRAINT fk_holidays_business FOREIGN KEY (business_id) REFERENCES businesses(id)
);
```

**Índices:**
- `business_id, holiday_date` UNIQUE

---

## Tabla: `staff`

```sql
CREATE TABLE staff (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  business_id UUID NOT NULL REFERENCES businesses(id) ON DELETE CASCADE,
  name VARCHAR(255) NOT NULL,
  email VARCHAR(255) UNIQUE,
  phone VARCHAR(20),
  
  -- Services this staff can handle (many-to-many)
  -- see staff_services table
  
  status VARCHAR(50) DEFAULT 'active',
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  
  CONSTRAINT fk_staff_business FOREIGN KEY (business_id) REFERENCES businesses(id)
);
```

---

## Tabla: `staff_services`

```sql
CREATE TABLE staff_services (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  staff_id UUID NOT NULL REFERENCES staff(id) ON DELETE CASCADE,
  service_id UUID NOT NULL REFERENCES services(id) ON DELETE CASCADE,
  
  CONSTRAINT fk_staff_service FOREIGN KEY (staff_id) REFERENCES staff(id),
  CONSTRAINT fk_staff_service_service FOREIGN KEY (service_id) REFERENCES services(id)
);
```

**Índices:**
- `staff_id, service_id` UNIQUE

---

## Tabla: `refresh_tokens`

```sql
CREATE TABLE refresh_tokens (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  token_hash VARCHAR(255) NOT NULL UNIQUE,
  expires_at TIMESTAMP NOT NULL,
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  
  CONSTRAINT fk_refresh_tokens_user FOREIGN KEY (user_id) REFERENCES users(id)
);
```

**Índices:**
- `user_id, expires_at`

---

## Tabla: `notification_logs`

```sql
CREATE TABLE notification_logs (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  reservation_id UUID NOT NULL REFERENCES reservations(id) ON DELETE CASCADE,
  type VARCHAR(50) NOT NULL, -- confirmation, reminder, cancellation
  channel VARCHAR(50) NOT NULL, -- email, sms, whatsapp
  recipient VARCHAR(255) NOT NULL,
  sent_at TIMESTAMP,
  status VARCHAR(50) DEFAULT 'pending', -- pending, sent, failed
  error_message TEXT,
  
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  
  CONSTRAINT fk_notification_logs_reservation FOREIGN KEY (reservation_id) REFERENCES reservations(id)
);
```

**Índices:**
- `reservation_id, type`
- `status, created_at` (para retry jobs)

---

## Tabla: `waitlists` (Por día, solo si no hay slots libres)

```sql
CREATE TABLE waitlists (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  service_id UUID NOT NULL REFERENCES services(id) ON DELETE CASCADE,
  waitlist_date DATE NOT NULL, -- Día para el que se espera
  
  -- Guest info
  user_id UUID REFERENCES users(id) ON DELETE CASCADE, -- NULL si es guest
  guest_id UUID REFERENCES guests(id) ON DELETE CASCADE, -- NULL si es user
  
  -- Posición en cola
  position INT NOT NULL, -- 1, 2, 3... para ese día
  
  -- Status
  status VARCHAR(50) DEFAULT 'waiting', -- waiting, notified, expired, joined
  notified_at TIMESTAMP, -- Cuándo se notificó (si se abrió un slot)
  
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  
  CONSTRAINT fk_waitlist_service FOREIGN KEY (service_id) REFERENCES services(id),
  CONSTRAINT fk_waitlist_user FOREIGN KEY (user_id) REFERENCES users(id),
  CONSTRAINT fk_waitlist_guest FOREIGN KEY (guest_id) REFERENCES guests(id),
  CONSTRAINT user_or_guest CHECK ((user_id IS NOT NULL AND guest_id IS NULL) OR (user_id IS NULL AND guest_id IS NOT NULL))
);
```

**Índices:**
- `service_id, waitlist_date, position` (para obtener siguiente en cola)
- `service_id, waitlist_date, status` (para queries)
- `user_id, waitlist_date` (para ver si user ya está en espera)
- `guest_id, waitlist_date` (para ver si guest ya está en espera)

**Lógica:**
1. Guest intenta reservar slot pero NO hay disponible
2. Sistema chequea: ¿hay slots libres ese día para ese servicio?
3. Si NO hay → se agrega a waitlist con `position = MAX(position) + 1`
4. Si SÍ hay → rechaza (no puede entrar en waitlist)
5. Cuando se cancela una reserva → notificar al primero en waitlist (status='notified')
6. Guest tiene 24h para confirmar o se mueve al siguiente

---

## Tabla: `audit_logs`

```sql
CREATE TABLE audit_logs (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  reservation_id UUID NOT NULL REFERENCES reservations(id) ON DELETE CASCADE,
  action VARCHAR(50) NOT NULL, -- created, updated, deleted, cancelled
  actor_id UUID REFERENCES users(id) ON DELETE SET NULL, -- NULL si guest
  guest_id UUID REFERENCES guests(id) ON DELETE SET NULL, -- NULL si user
  actor_type VARCHAR(50), -- owner, registered_user, guest, system
  old_values JSONB, -- snapshot antes del cambio
  new_values JSONB, -- snapshot después del cambio
  ip_address VARCHAR(45), -- IPv4 o IPv6
  user_agent TEXT,
  
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  
  CONSTRAINT fk_audit_logs_reservation FOREIGN KEY (reservation_id) REFERENCES reservations(id),
  CONSTRAINT fk_audit_logs_actor FOREIGN KEY (actor_id) REFERENCES users(id),
  CONSTRAINT fk_audit_logs_guest FOREIGN KEY (guest_id) REFERENCES guests(id)
);
```

**Índices:**
- `reservation_id, created_at`
- `actor_id, created_at`
- `action, created_at`

**Ejemplo de datos:**
```json
{
  "action": "updated",
  "actor_id": "guest-token-abc",
  "actor_type": "guest",
  "old_values": {"status": "pending", "start_time": "2026-06-20T14:00:00Z"},
  "new_values": {"status": "confirmed", "start_time": "2026-06-20T15:00:00Z"},
  "ip_address": "192.168.1.1",
  "created_at": "2026-06-20T10:30:00Z"
}
```

---

## Flujo: Invitado → Usuario Registrado

**Caso:** Invitado reserva con teléfono → Luego se registra y sincroniza historial

### Paso 1: Invitado reserva
```
POST /reservations (guest)
{
  "guestName": "Juan Pérez",
  "guestPhone": "+34912345678",
  "serviceId": "uuid"
}
```

Backend:
1. Crear/recuperar `guest` con phone_encrypted = "+34912345678"
2. Crear `reservation` con guest_id = "guest-uuid-1", user_id = NULL

Tabla `guests`:
```sql
-- id: guest-uuid-1
-- name: "Juan Pérez"
-- phone_encrypted: "AES(+34912345678)"
-- user_id: NULL (aún no registrado)
```

Tabla `reservations`:
```sql
-- id: uuid-1
-- guest_id: "guest-uuid-1"
-- user_id: NULL
-- status: pending
```

### Paso 2: Invitado se registra (AUTO-SYNC)
```
POST /auth/register
{
  "email": "juan@example.com",
  "password": "...",
  "phone": "+34912345678",
  "name": "Juan Pérez"
}
```

Backend (flujo automático):
```csharp
1. Validar email único
2. Crear user con:
   - type = 'customer' (por defecto, puede cambiar a 'owner' después)
   - email, password_hash, phone, name
   
3. Buscar guest con phone_encrypted = ENCRYPT("+34912345678")
   FOR EACH negocio donde guest tiene reservas:
     UPDATE guests SET user_id = "user-uuid-2" 
     WHERE phone_encrypted = AES("+34912345678")
     
4. Retornar user con lista de negocio donde ya tiene reservas
```

Tabla `guests` después:
```sql
-- id: guest-uuid-1
-- user_id: "user-uuid-2"  ← Linkado automáticamente
-- all reservations already visible via user_id
```

**Nota:** Si el guest usó email en un negocio y teléfono en otro, hacer búsqueda en ambos campos y linkear todos.

### Paso 3: Automático - Ver Historial
```
GET /my-reservations

Backend:
SELECT * FROM reservations 
WHERE user_id = "user-uuid-2" OR guest_id IN (
  SELECT id FROM guests WHERE user_id = "user-uuid-2"
)
```

**Beneficio:**
- Invitado no pierde historial
- Sincronización automática (sin endpoint extra)
- Guest como entidad propia permite tracking de cliente
- Phone/email encriptados
- Unificación transparente

---

## Encryption Strategy

- **guest_phone_encrypted, guest_email_encrypted:** AES-256-GCM
- **Key rotation:** Anual (futuro)
- **Per-column IV/Salt:** Almacenado en el valor encriptado

```csharp
// Ejemplo en C#
string encrypted = CryptoService.Encrypt(plaintext, masterKey);
string decrypted = CryptoService.Decrypt(encrypted, masterKey);
```

---

## Migrations Strategy

Archivo: `Slotify.Infrastructure/Migrations/` (EF Core)

```
001_InitialCreate.cs
002_AddBusinessHours.cs
003_AddStaffTable.cs
...
```

Cada migration:
- `Up()`: cambios
- `Down()`: rollback

```csharp
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder mb)
    {
        mb.CreateTable("users", t => { ... });
    }
    
    protected override void Down(MigrationBuilder mb)
    {
        mb.DropTable("users");
    }
}
```
