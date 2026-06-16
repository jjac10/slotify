# Slotify

App de reservas local para negocios (estilo Booksy). Proyecto TFM desarrollado con
metodología **TDD estricta** asistida por IA.

## Stack
- **Backend:** ASP.NET Core 10 + Entity Framework Core 10 + PostgreSQL 17
- **Frontend:** React 19 + TypeScript + Vite
- **Testing:** xUnit + Moq + Testcontainers (backend) · Vitest + RTL + Playwright (frontend)
- **Infra:** Docker + Docker Compose + GitHub Actions

## Estado del proyecto

**Fase 3 (Desarrollo TDD)** en curso · **61/61 tests en verde**.

Implementado y probado: capa de datos (`pricing_tiers`, `users`, `businesses`, `staff`,
`services`, `refresh_tokens`), *owner-as-staff* atómico, límites Freemium (staff y servicios),
**autenticación completa** (registro/login/refresh/`me` con JWT) y **CRUD de servicios**
(alta owner-only, listado público, `GET /businesses`). La API se levanta con
`docker-compose up` (UI Scalar en `/scalar`).

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
