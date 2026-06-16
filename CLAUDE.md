# CLAUDE.md - Slotify TFM Project

## Descripción
Slotify es una aplicación de reservas local estilo Booksy, desarrollada con metodología TDD asistida por IA para demostrar software profesional de calidad.

## Stack Tecnológico
- **Backend:** ASP.NET Core 10 + Entity Framework Core + PostgreSQL
- **Frontend:** React 19 + TypeScript + Vite
- **Testing:** xUnit + Moq (backend), Vitest + React Testing Library + Playwright (frontend)
- **Infra:** Docker + Docker Compose + GitHub Actions
- **VPS:** Ionos con PostgreSQL en contenedor

## Arquitectura
- **Monorepo:** Un único repo con /backend, /frontend, /infra
- **Backend:** Repository Pattern + Dependency Injection (BD intercambiable)
- **Database:** Code First + EF Core Migrations (versioning en git)
- **Testing:** TDD strict — tests primero, código después
- **Git:** Conventional Commits (feat:, test:, fix:, docs:, infra:)

## Decisiones de Diseño
- **Flujo Invitado:** Toggle Teléfono (default) | Email
- **Plan Freemium:** Free (100 reservas/mes, 50 clientes, 5 servicios, 1 trabajador) + Premium
- **BD Flexible:** Repository Pattern permite cambiar PostgreSQL sin tocar lógica
- **Logo:** Clock & Slot (Morado #7C3AED + Cyan #06B6D4)
- **Doble booking:** Unique constraint + optimistic locking en BD
- **Seguridad:** Teléfono/email invitados encriptados AES-256
- **Zonas horarias:** UTC en BD, local en UI

## Fases
1. ✅ **Fase 0:** Naming, Freemium, Flujo Invitado, Logo
2. **Fase 1:** Stack Tecnológico (actual)
3. **Fase 2:** Modelo de Datos + Setup Monorepo + Docker
4. **Fase 3:** Desarrollo Incremental TDD + Commits atómicos
5. **Fase 4:** Producción + Despliegue Ionos

## Convenciones de Código
- **C#:** PascalCase para clases/métodos, camelCase para variables
- **TypeScript:** PascalCase para componentes, camelCase para funciones
- **Git Commits:** `feat(auth): add login endpoint` o `test(reservations): add concurrent booking tests`
- **DB Migrations:** `Add_ClientsTable` o `UpdateReservationSchema`

## Extensibilidad
Todos los sistemas (pricing tiers, features, integraciones) diseñados para evolucionar sin refactoring mayor.
