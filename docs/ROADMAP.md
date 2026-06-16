# Roadmap & Checklist — Slotify

> Planificación viva del proyecto. Marca dónde estamos y qué falta.
> Esquema canónico de BD: [`DATA_MODEL.md`](./DATA_MODEL.md) · Decisiones: [`DECISIONS.md`](./DECISIONS.md)

**Leyenda:** ✅ hecho · 🚧 en curso · ⬜ pendiente · 🔮 futuro (fuera de MVP)

---

## Estado actual

- **Fase activa:** 3 (Desarrollo incremental TDD).
- **Tests:** 32/32 en verde (xUnit + Moq + Testcontainers PostgreSQL 17 + WebApplicationFactory).
- **Lo que ya funciona (probado):** seed de planes, alta de negocio con owner-as-staff atómico, validación de límite Freemium de staff, y **autenticación completa** (registro de owner, login, refresh con rotación, `/me` protegido por JWT).
- **Ya se puede ver en navegador:** `Slotify.API` levanta con `docker-compose up` → UI Scalar en `/scalar`, OpenAPI en `/openapi/v1.json`.

---

## Fases del proyecto

- ✅ **Fase 0 — Producto:** naming, plan Freemium, flujo invitado, logo.
- ✅ **Fase 1 — Stack:** ASP.NET Core 10, EF Core, PostgreSQL, React 19, Docker.
- 🚧 **Fase 2 — Setup monorepo + Docker**
  - ✅ Estructura monorepo (`backend/`, `frontend/`, `infra/`, `docs/`)
  - ✅ `docker-compose.yml` (postgres + backend + frontend) y `infra/Dockerfile.*`
  - ✅ Scaffold backend (`Slotify.slnx`: Domain, Infrastructure, Tests) + aislamiento NuGet
  - ✅ Proyecto `Slotify.API` (host ASP.NET + OpenAPI/Scalar + JWT + migrate-on-startup)
  - ⬜ CI/CD GitHub Actions (build + test en cada push)
  - ⬜ Scaffold frontend (Vite + React 19 + TS)
- 🚧 **Fase 3 — Desarrollo TDD** (ver detalle abajo)
- 🔮 **Fase 4 — Producción:** despliegue Ionos, HTTPS, dominio.

---

## Capa de datos (entidades + migraciones)

Comparado con [`DATA_MODEL.md`](./DATA_MODEL.md):

- ✅ `pricing_tiers` (+ seed free/premium) — *PR #1*
- ✅ `users` (mínimo: identidad, type, status) — *PR #1*
- ✅ `businesses` (mínimo: owner, tier, name, status) — *PR #1*
  - ⬜ columnas restantes (contacto, ubicación, personalización, config, social, stats)
- ✅ `staff` (+ owner-as-staff) — *PR #2*
- ⬜ `services`
- ⬜ `staff_services`
- ⬜ `guests` (AES-256-GCM + HMAC blind index)
- ⬜ `reservations` (+ unique index anti-doble-booking por staff, optimistic locking)
- ⬜ `business_hours` · ⬜ `business_holidays`
- ✅ `refresh_tokens` — *PR #5* · ⬜ `password_reset_tokens` · ⬜ `confirmation_tokens`
- ⬜ `notification_logs` · ⬜ `waitlists` · ⬜ `audit_logs` · ⬜ `reviews`
- 🔮 `payments` (esqueleto documentado, no MVP)

---

## Lógica de negocio (servicios + repositorios)

- ✅ `BusinessService.CreateAsync` → crea negocio + owner-staff atómico — *PR #2*
  - ✅ `IBusinessRepository` / `BusinessRepository` (EF)
- ✅ `FreemiumLimitService.CanAddStaffAsync` (data-driven, ADR #9) — *PR #3*
  - ✅ `ITierRepository` / `IStaffRepository` (+ impl. EF)
  - ⬜ `CanAddServiceAsync`, `CanAddReservationThisMonthAsync`, `CanAddClientAsync`
- ✅ Auth: registro (bcrypt), login (JWT HS256), refresh con rotación — *PR #5*
  - ✅ `IPasswordHasher`/bcrypt, `ITokenService`/JWT, `AuthService`, repos EF (`AuthRepository`, `RefreshTokenRepository`)
  - ⬜ reset password (password_reset_tokens)
- ⬜ Servicios (CRUD) con límite Freemium
- ⬜ Disponibilidad (slots) respetando horario, festivos, ocupación, timezone
- ⬜ Reservas: crear (guest/user), anti-doble-booking, modificar, cancelar (hard delete + audit)
- ⬜ Guests: cifrado + blind index + sync invitado→usuario automática
- ⬜ Notificaciones (async fire & forget), audit logs, reviews, waitlist

---

## API (host + endpoints)

- ✅ `Slotify.API`: `Program.cs`, DI (DbContext + repos + servicios), OpenAPI/Scalar, JWT, migraciones al arranque
- ✅ `POST /auth/register` · ✅ `POST /auth/login` · ✅ `POST /auth/refresh` · ✅ `GET /auth/me` (protegido)
- ⬜ `GET/POST /businesses/{id}/services`
- ⬜ `GET /businesses/{id}/availability`
- ⬜ `POST/GET/PATCH/DELETE /reservations`
- ⬜ Dashboard owner · ⬜ rate limiting · ⬜ manejo de errores estándar (middleware)

---

## Frontend

- ⬜ Scaffold Vite + React 19 + TS (strict) · ⬜ cliente API (axios)
- ⬜ Flujo de reserva (toggle teléfono/email) · ⬜ slots disponibles
- ⬜ Auth (login/registro) · ⬜ Dashboard owner · ⬜ PWA + responsive
- ⬜ Tests Vitest + RTL + Playwright (E2E)

---

## Infra / Calidad

- ⬜ CI/CD GitHub Actions (build + test)
- ⬜ Fijar versión parcheada de `System.Security.Cryptography.Xml` (warning NU1903, transitivo vía EF Design)
- 🔮 Despliegue Ionos, backups, HTTPS

---

## Historial de PRs (Fase 3)

| PR | Rama | Contenido |
|----|------|-----------|
| #1 | `feature/data-layer-pricing-tiers-businesses` | Entidades + migración `InitialCreate` (users, pricing_tiers, businesses) + seed |
| #2 | `feature/staff-owner-as-staff` | `staff` + `BusinessService`/`BusinessRepository` (owner-as-staff) |
| #3 | `feature/freemium-limits` | `FreemiumLimitService` + repos EF (límite de staff) |
| #4 | `docs/project-roadmap` | Roadmap + READMEs |
| #5 | `feature/api-auth-jwt` | `Slotify.API` + auth completa (register/login/refresh/me) con JWT + bcrypt |

---

## Siguiente paso

🎯 **Servicios (CRUD) con límite Freemium**: `services` (tabla + entidad), `CanAddServiceAsync`, y endpoints `GET/POST /businesses/{id}/services` protegidos (owner). Alternativas: `guests` + `reservations` (núcleo de reservas), o scaffold del frontend para empezar a consumir la API.
