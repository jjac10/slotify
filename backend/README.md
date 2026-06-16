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

- Entidades: `PricingTier`, `User`, `Business`, `Staff`, `RefreshToken`.
- `SlotifyDbContext` (mapeo snake_case, FKs, índices, seed free/premium).
- Migraciones: `InitialCreate`, `AddStaff`, `AddRefreshTokens`.
- Servicios: `BusinessService` (owner-as-staff), `FreemiumLimitService`, `AuthService` (register/login/refresh).
- Seguridad: `BcryptPasswordHasher`, `JwtTokenService` (JWT HS256).
- Repositorios EF: `BusinessRepository`, `TierRepository`, `StaffRepository`, `AuthRepository`, `RefreshTokenRepository`.
- Endpoints: `POST /auth/register`, `POST /auth/login`, `POST /auth/refresh`, `GET /auth/me` (protegido).

## Ejecutar la API

Con `docker-compose up` desde la raíz (postgres + API). La API migra al arrancar.
- API: `http://localhost:5000`
- UI Scalar (dev): `http://localhost:5000/scalar`
- OpenAPI JSON: `http://localhost:5000/openapi/v1.json`

## Testing

Requiere **Docker en marcha** (los tests de integración levantan PostgreSQL con Testcontainers).

```bash
cd backend
dotnet test          # 32/32 verde
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
