# Arquitectura de Slotify

## Visión General
Slotify es un monorepo con arquitectura por capas:
- **Frontend:** React SPA (PWA-ready)
- **Backend:** ASP.NET Core API (Clean Architecture)
- **Database:** PostgreSQL con Code First

## Stack Tecnológico

### Backend
```
ASP.NET Core 10
├── Controllers (API REST)
├── Services (Lógica de negocio)
├── Repositories (Acceso a datos)
└── Domain (Entities + Interfaces)
```

**Pattern:** Repository + Dependency Injection
- Abstraer BD detrás de `IRepository<T>`
- Cambiar PostgreSQL a otra BD = nueva implementación

### Frontend
```
React 19 + TypeScript
├── Components (UI reutilizable)
├── Pages (Rutas)
├── Hooks (Lógica compartida)
├── Services (API calls)
└── Types (TypeScript interfaces)
```

**Build:** Vite (más rápido que Webpack)
**Styling:** TBD (CSS Modules o Tailwind)

### Database
**Engine:** PostgreSQL 17 (ACID, JSON support, constraints fuertes)
**Approach:** Code First + EF Core Migrations

**Ventajas:**
- Migrations en git (versionable)
- C# como fuente de verdad
- Fácil cambiar a MySQL, SQL Server si se necesita

## Convenciones

### Naming
- **C#:** `PascalCase` (clases, métodos), `camelCase` (variables)
- **TypeScript:** `PascalCase` (componentes), `camelCase` (funciones)
- **SQL:** `snake_case` (tablas, columnas)

### Git Commits
```
feat(auth): add login endpoint
test(reservations): add concurrent booking tests
fix(schedule): prevent double-booking race condition
docs(api): add endpoint documentation
infra(docker): update postgres version to 17
```

## Fase de Desarrollo

### Fase 2 (Actual): Setup
- [ ] Crear estructura base del monorepo
- [ ] Configurar Docker Compose local
- [ ] Setup CI/CD en GitHub Actions

### Fase 3: TDD Development
Cada feature = Test + Código
```
1. Escribir test (red)
2. Código mínimo que pase (green)
3. Refactor (refactor)
4. Commit atómico
```

### Fase 4: Producción
- Desplegar en VPS Ionos
- PostgreSQL en contenedor en Ionos
- HTTPS + dominio propio
