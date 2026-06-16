# Slotify Backend

ASP.NET Core 10 + EF Core 10 sobre PostgreSQL 17, con Repository Pattern + DI (ADR #2).

## Stack
- **Framework:** ASP.NET Core 10
- **ORM:** Entity Framework Core 10 (Code First + Migrations)
- **Database:** PostgreSQL 17 (Npgsql)
- **Testing:** xUnit + Moq + Testcontainers

## Estructura

```
backend/
├── Slotify.slnx              # Solución (formato .slnx, .NET 10)
├── Slotify.API/              # Host ASP.NET: Program.cs, Controllers, OpenAPI/Scalar, JWT
├── Slotify.Domain/           # Entities, Interfaces, Services, DTOs, Exceptions (sin EF)
├── Slotify.Infrastructure/   # DbContext, Migrations, Repositories, Security
└── Slotify.Tests/            # Unit (Moq) + Integration (Testcontainers + WebApplicationFactory)
```

## Implementado

- Entidades: `PricingTier`, `User`, `Business`, `Staff`, `Service`, `Guest`, `Reservation`, `BusinessHour`, `BusinessHoliday`, `RefreshToken`, `AuditLog`.
- `SlotifyDbContext` (mapeo snake_case, FKs, índices, CHECKs, exclusion constraint, seed free/premium).
- Migraciones: `InitialCreate`, `AddStaff`, `AddRefreshTokens`, `AddServices`, `AddGuests`, `AddReservations`, `AddBusinessHours`, `AddBusinessHolidays`, `AddAuditLogs`.
- Servicios: `BusinessService`, `FreemiumLimitService`, `ServiceService`, `AuthService`, `BookingService`, `ReservationManagementService` (cancelar/reprogramar), `BusinessScheduleService`, `AvailabilityService`, `PasswordPolicy`.
- Seguridad: `BcryptPasswordHasher`, `JwtTokenService` (JWT HS256), `AesGcmCryptoService` + `HmacBlindIndex` (ADR #5).
- Repositorios EF: `Business`, `Tier`, `Staff`, `Service`, `Guest`, `Reservation`, `BusinessHour`, `BusinessHoliday`, `Auth`, `RefreshToken`, `AuditLog`.
- Endpoints: auth, `GET /businesses`, servicios, reservas (`POST`/`GET`/`PATCH`/`DELETE /reservations/{id}`: crear, consultar, reprogramar, cancelar), horario (`hours`/`holidays`), disponibilidad (`GET /businesses/{id}/availability`).

## Ejecutar la API

Con `docker-compose up` desde la raíz (postgres + API). La API migra al arrancar.
- API: `http://localhost:5000`
- UI Scalar (dev): `http://localhost:5000/scalar`
- OpenAPI JSON: `http://localhost:5000/openapi/v1.json`

## Testing

Requiere **Docker en marcha** (los tests de integración levantan PostgreSQL con Testcontainers).

```bash
cd backend
dotnet test          # 139/139 verde
```

## Migraciones (EF Core)

Herramienta: `dotnet ef` **10.x** (`dotnet tool update --global dotnet-ef --version 10.0.0`).

```bash
# crear una migración (DbContext en Slotify.Infrastructure)
dotnet ef migrations add <Nombre> --project Slotify.Infrastructure --output-dir Migrations

# aplicar (cuando exista el host o vía script); en tests se aplica con Database.Migrate()
dotnet ef database update --project Slotify.Infrastructure
```

## Nota sobre NuGet

El repo incluye un `NuGet.config` que aísla las fuentes a nuget.org (evita feeds
privados heredados de la máquina que rompían el restore).
