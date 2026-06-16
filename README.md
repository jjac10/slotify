# Slotify

App de reservas local para negocios (estilo Booksy). Proyecto TFM desarrollado con
metodología **TDD estricta** asistida por IA.

## Stack
- **Backend:** ASP.NET Core 10 + Entity Framework Core 10 + PostgreSQL 17
- **Frontend:** React 19 + TypeScript + Vite
- **Testing:** xUnit + Moq + Testcontainers (backend) · Vitest + RTL + Playwright (frontend)
- **Infra:** Docker + Docker Compose + GitHub Actions

## Estado del proyecto

**Fase 3 (Desarrollo TDD)** en curso · **114/114 tests en verde**.

Implementado y probado: auth completa (customer/owner, login, refresh, `/me` con JWT),
negocios y servicios (CRUD con límites Freemium), **núcleo de reservas** (invitado cifrado
o usuario, anti-doble-booking robusto), **horario del negocio** (horarios + festivos) y
**disponibilidad** (`GET /availability`: slots = horario − festivos − reservas). Flujo de
reserva completo de punta a punta. La API se levanta con `docker-compose up` (Scalar en `/scalar`).

👉 Planificación completa y checklist: [`docs/ROADMAP.md`](docs/ROADMAP.md)

## Estructura

```
slotify/
├── backend/      # ASP.NET Core (Domain, Infrastructure, Tests; API próximamente)
├── frontend/     # React 19 + TS + Vite (pendiente de scaffold)
├── infra/        # Dockerfiles + nginx
├── docs/         # Documentación (ver índice abajo)
└── docker-compose.yml
```

## Setup local

Requisitos: Docker Desktop, .NET 10 SDK, Node 22+.

```bash
git clone https://github.com/jjac10/slotify.git
cd slotify
docker-compose up --build   # postgres + (backend/frontend cuando existan)
```

> Los tests de integración del backend usan **Testcontainers**, así que necesitan
> **Docker en marcha**. Ejecutar la suite:
> ```bash
> cd backend
> dotnet test
> ```

## Documentación

- [ROADMAP](docs/ROADMAP.md) — planificación y checklist
- [REQUIREMENTS](docs/REQUIREMENTS.md) — requisitos y casos de uso
- [DATA_MODEL](docs/DATA_MODEL.md) — esquema canónico de BD
- [DECISIONS](docs/DECISIONS.md) — ADRs (decisiones de arquitectura)
- [ARCHITECTURE](docs/ARCHITECTURE.md) · [API](docs/API.md) · [DATABASE](docs/DATABASE.md)
- [SETUP](docs/SETUP.md) · [DEVELOPMENT](docs/DEVELOPMENT.md) · [GITFLOW](GITFLOW.md)
