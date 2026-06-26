# Database — Slotify

> ⚠️ **El esquema canónico y completo está en [`DATA_MODEL.md`](./DATA_MODEL.md).**
> Este archivo solo cubre el enfoque y los comandos. No dupliques DDL aquí
> (evita que el modelo se desincronice y confunda a la generación asistida por IA).

## Enfoque

- **Code First + EF Core Migrations** (C# como source of truth, migraciones en git).
- **PostgreSQL** (ACID para reservas concurrentes, JSONB, constraints fuertes).
- **Repository Pattern + DI** para mantener la BD intercambiable sin tocar servicios.

Decisiones razonadas en [`DECISIONS.md`](./DECISIONS.md) (ADRs).

## Comandos de migración

```bash
cd backend
dotnet ef migrations add InitialCreate   # crear migración
dotnet ef database update                # aplicar
dotnet ef migrations remove              # deshacer la última (no aplicada)
```

## Datos sensibles

- Teléfono/email de invitados: **AES-256-GCM** (valor recuperable) + **HMAC-SHA256**
  (blind index para búsqueda/unicidad). Ver sección *Encryption Strategy* en `DATA_MODEL.md`.
- Contraseñas: hash con bcrypt.
- Pagos: tabla `payments` documentada como futura (PCI-DSS ready cuando se implemente).

## Seed inicial

La primera migration debe sembrar `pricing_tiers` (free / premium). Ver `DATA_MODEL.md`.
