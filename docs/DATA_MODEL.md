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
  plan VARCHAR(50) DEFAULT 'free', -- free, premium
  status VARCHAR(50) DEFAULT 'active', -- active, inactive, deleted
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  deleted_at TIMESTAMP NULL
);
```

**Índices:**
- `email` UNIQUE
- `status, created_at`

---

## Tabla: `businesses`

```sql
CREATE TABLE businesses (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  owner_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  name VARCHAR(255) NOT NULL,
  description TEXT,
  phone VARCHAR(20),
  email VARCHAR(255),
  address TEXT,
  timezone VARCHAR(50) DEFAULT 'UTC',
  
  -- Customization
  logo_url TEXT,
  primary_color VARCHAR(7), -- #RRGGBB
  secondary_color VARCHAR(7),
  
  -- Config
  max_slots_per_service INT DEFAULT 10,
  cancellation_hours INT DEFAULT 24, -- min horas antes de cancelar
  
  status VARCHAR(50) DEFAULT 'active',
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  
  CONSTRAINT fk_businesses_owner FOREIGN KEY (owner_id) REFERENCES users(id)
);
```

**Índices:**
- `owner_id`
- `name`
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

## Tabla: `reservations`

```sql
CREATE TABLE reservations (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  business_id UUID NOT NULL REFERENCES businesses(id) ON DELETE CASCADE,
  service_id UUID NOT NULL REFERENCES services(id) ON DELETE RESTRICT,
  
  -- Guest info (can be logged in user or anonymous)
  guest_id UUID REFERENCES users(id) ON DELETE SET NULL, -- NULL = invitado
  guest_name VARCHAR(255) NOT NULL,
  guest_phone_encrypted VARCHAR(255), -- encrypted si invitado
  guest_email_encrypted VARCHAR(255), -- encrypted si invitado
  
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
  CONSTRAINT fk_reservations_guest FOREIGN KEY (guest_id) REFERENCES users(id),
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

## Tabla: `audit_logs`

```sql
CREATE TABLE audit_logs (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  reservation_id UUID NOT NULL REFERENCES reservations(id) ON DELETE CASCADE,
  action VARCHAR(50) NOT NULL, -- created, updated, deleted, cancelled
  actor_id UUID REFERENCES users(id) ON DELETE SET NULL, -- NULL si invitado
  actor_type VARCHAR(50), -- owner, guest, system
  old_values JSONB, -- snapshot antes del cambio
  new_values JSONB, -- snapshot después del cambio
  ip_address VARCHAR(45), -- IPv4 o IPv6
  user_agent TEXT,
  
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  
  CONSTRAINT fk_audit_logs_reservation FOREIGN KEY (reservation_id) REFERENCES reservations(id),
  CONSTRAINT fk_audit_logs_actor FOREIGN KEY (actor_id) REFERENCES users(id)
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

**Caso:** Invitado reserva → Luego se registra y quiere ver su historial

### Paso 1: Invitado reserva
```
POST /reservations
{
  "guestName": "Juan Pérez",
  "guestPhone": "+34912345678",  // encrypted en BD
  "serviceId": "uuid"
}

Response:
{
  "reservationId": "uuid-1",
  "confirmationToken": "token-abc-123"
}
```

Tabla `reservations`:
```sql
-- reservation-id: uuid-1
-- guest_id: NULL (invitado)
-- guest_phone_encrypted: "AES(+34912345678)"
-- status: pending
```

### Paso 2: Invitado se registra
```
POST /auth/register
{
  "email": "juan@example.com",
  "password": "...",
  "phone": "+34912345678"  // mismo teléfono
}

Response:
{
  "userId": "user-uuid-2",
  "accessToken": "jwt-token"
}
```

### Paso 3: Sincronización (Manual o API)
```
POST /auth/link-reservations
{
  "phone": "+34912345678"  // o email
}

Backend:
1. User logueado = "user-uuid-2"
2. Buscar reservations donde guest_phone_encrypted = ENCRYPT("+34912345678")
3. UPDATE reservations SET guest_id = "user-uuid-2" WHERE ...
4. Retornar lista de reservas sincronizadas

Response:
{
  "syncedReservations": [
    {"id": "uuid-1", "service": "Corte", "date": "2026-06-20"}
  ]
}
```

Tabla `reservations` después:
```sql
-- reservation-id: uuid-1
-- guest_id: "user-uuid-2"  ← Ahora linkado
-- guest_phone_encrypted: "AES(+34912345678)"  ← Aún encriptado
-- status: confirmed
```

**Beneficio:**
- Invitado no pierde historial al registrarse
- Guest_phone sigue encriptado (privacy)
- User puede ver todas sus reservas (logged in + guest)

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
