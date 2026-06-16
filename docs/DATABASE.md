# Database Design - Slotify

## Enfoque: Code First + EF Core Migrations

### ¿Por qué Code First?
- **Versionable:** Migrations en git, historial completo
- **Flexible:** Cambiar de BD sin perder schema
- **TDD-friendly:** Define tabla → test → código

### ¿Por qué PostgreSQL?
- ACID guarantees (crítico para reservas concurrentes)
- JSON support (futuro: datos dinámicos de negocio)
- Constraints fuertes (integridad de datos)
- Escala bien en producción

## Estructura de BD

### Core Entities

#### Users
```sql
users (
  id UUID PRIMARY KEY,
  name VARCHAR(255) NOT NULL,
  email VARCHAR(255) UNIQUE,
  phone VARCHAR(20) UNIQUE,
  created_at TIMESTAMP DEFAULT NOW()
)
```

#### Businesses
```sql
businesses (
  id UUID PRIMARY KEY,
  owner_id UUID FOREIGN KEY,
  name VARCHAR(255) NOT NULL,
  description TEXT,
  timezone VARCHAR(50) DEFAULT 'UTC',
  created_at TIMESTAMP DEFAULT NOW()
)
```

#### Services
```sql
services (
  id UUID PRIMARY KEY,
  business_id UUID FOREIGN KEY NOT NULL,
  name VARCHAR(255) NOT NULL,
  duration_minutes INT NOT NULL,
  price DECIMAL(10,2),
  created_at TIMESTAMP DEFAULT NOW()
)
```

#### Reservations
```sql
reservations (
  id UUID PRIMARY KEY,
  business_id UUID FOREIGN KEY NOT NULL,
  service_id UUID FOREIGN KEY NOT NULL,
  client_name VARCHAR(255) NOT NULL,
  client_phone VARCHAR(20),
  client_email VARCHAR(255),
  start_time TIMESTAMP NOT NULL,
  end_time TIMESTAMP NOT NULL,
  status VARCHAR(50) DEFAULT 'pending',
  notes TEXT,
  created_at TIMESTAMP DEFAULT NOW()
)
```

## Migrations

Crear migración:
```bash
cd backend
dotnet ef migrations add InitialCreate
```

Aplicar cambios:
```bash
dotnet ef database update
```

Deshacer última migración:
```bash
dotnet ef migrations remove
```

## Repository Pattern para Extensibilidad

```csharp
public interface IRepository<T> where T : Entity
{
    Task<T> GetByIdAsync(Guid id);
    Task<IEnumerable<T>> GetAllAsync();
    Task AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(Guid id);
}
```

**Ventaja:** Cambiar a MongoDB o SQL Server = nueva implementación sin tocar servicios.

## Datos Sensibles (Encriptación)

- **Teléfono + Email de invitados:** Encriptados en BD
- **Contraseñas:** Hashed con bcrypt (cuando exista auth)
- **Pagos:** PCI-DSS ready (cuando se implemente)

```csharp
// Ejemplo: encriptar email invitado
string encryptedEmail = CryptoService.Encrypt(guestEmail);
```

## Índices y Optimización

Índices clave:
```sql
CREATE INDEX idx_reservations_business_start 
  ON reservations(business_id, start_time);

CREATE INDEX idx_reservations_client_phone 
  ON reservations(client_phone);
```
