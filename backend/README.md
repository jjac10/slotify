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
├── Slotify.Domain/           # Entities, Interfaces, Services, DTOs (sin dependencia de EF)
├── Slotify.Infrastructure/   # DbContext, Migrations, Repositories
└── Slotify.Tests/            # Unit (Moq) + Integration (Testcontainers)
```

> El proyecto `Slotify.API` (host + controllers + Program.cs) aún no existe; se
> añade en el hito *api-walking-skeleton*. Ver [ROADMAP](../docs/ROADMAP.md).

## Implementado

- Entidades: `PricingTier`, `User`, `Business`, `Staff`.
- `SlotifyDbContext` (mapeo snake_case, FKs, índices, seed free/premium).
- Migraciones: `InitialCreate`, `AddStaff`.
- Servicios: `BusinessService` (owner-as-staff), `FreemiumLimitService` (límite de staff).
- Repositorios EF: `BusinessRepository`, `TierRepository`, `StaffRepository`.

## Testing

Requiere **Docker en marcha** (los tests de integración levantan PostgreSQL con Testcontainers).

```bash
cd backend
dotnet test          # 13/13 verde
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
