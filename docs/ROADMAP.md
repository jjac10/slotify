# Roadmap & Checklist — Slotify

> Planificación viva del proyecto. Marca dónde estamos y qué falta.
> Esquema canónico de BD: [`DATA_MODEL.md`](./DATA_MODEL.md) · Decisiones: [`DECISIONS.md`](./DECISIONS.md)

**Leyenda:** ✅ hecho · 🚧 en curso · ⬜ pendiente · 🔮 futuro (fuera de MVP)

---

## Estado actual

- **Fase activa:** 3 (Desarrollo incremental TDD).
- **Tests:** 117/117 en verde (xUnit + Moq + Testcontainers PostgreSQL 17 + WebApplicationFactory).
- **Lo que ya funciona (probado):** auth completa, negocios + servicios (CRUD con límite Freemium), **núcleo de reservas** (invitado cifrado o usuario, anti-doble-booking robusto), **horario del negocio** (horarios + festivos) y **disponibilidad** (`GET /availability` con slots = horario − festivos − reservas, paso configurable). Flujo de reserva completo de punta a punta.
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
- ✅ `services` — *PR #6*
- ⬜ `staff_services`
- ✅ `guests` (AES-256-GCM + HMAC blind index) — *PR #9*
- ✅ `reservations` (+ exclusion constraint gist anti-doble-booking, optimistic locking) — *PR #9*
- ✅ `business_hours` · ✅ `business_holidays` — *PR #10*
- ✅ `refresh_tokens` — *PR #5* · ⬜ `password_reset_tokens` · ⬜ `confirmation_tokens`
- ⬜ `notification_logs` · ⬜ `waitlists` · ⬜ `audit_logs` · ⬜ `reviews`
- 🔮 `payments` (esqueleto documentado, no MVP)

---

## Lógica de negocio (servicios + repositorios)

- ✅ `BusinessService.CreateAsync` → crea negocio + owner-staff atómico — *PR #2*
  - ✅ `IBusinessRepository` / `BusinessRepository` (EF)
- ✅ `FreemiumLimitService` (data-driven, ADR #9): `CanAddStaffAsync` — *PR #3*, `CanAddServiceAsync` — *PR #6*
  - ✅ `ITierRepository` / `IStaffRepository` / `IServiceRepository` (+ impl. EF)
  - ⬜ `CanAddReservationThisMonthAsync`, `CanAddClientAsync`
- ✅ `ServiceService` (alta owner-only + límite, listado) — *PR #6*; `BusinessService.ListByOwnerAsync`
- ✅ Auth: registro (bcrypt + **política de contraseña segura** *PR #7*), login (JWT HS256), refresh con rotación — *PR #5*
  - ✅ `IPasswordHasher`/bcrypt, `ITokenService`/JWT, `AuthService`, `PasswordPolicy`, repos EF (`AuthRepository`, `RefreshTokenRepository`)
  - ⬜ reset password (password_reset_tokens)
- ✅ `BookingService` (crear guest/user, endTime, dedupe, overlap) + `CryptoService`/`BlindIndex` — *PR #9*
- ✅ `BusinessScheduleService` (horario semanal + festivos, owner-only, validación) — *PR #10*
- ✅ `AvailabilityService` (slots = horario − festivos − reservas, paso configurable) — *PR #11* · ⬜ timezone por negocio, anti-huecos avanzado
- ⬜ `CanAddReservationThisMonthAsync` (límite Freemium de reservas)
- ✅ Reservas: crear (guest/user) con anti-doble-booking — *PR #9* · ⬜ modificar, cancelar (hard delete + audit)
- ✅ Guests: cifrado + blind index — *PR #9* · ✅ sync invitado→usuario automática (al registrarse, por blind index) — *PR #12*
- ⬜ Notificaciones (async fire & forget), audit logs, reviews, waitlist

---

## API (host + endpoints)

- ✅ `Slotify.API`: `Program.cs`, DI (DbContext + repos + servicios), OpenAPI/Scalar, JWT, migraciones al arranque
- ✅ `POST /auth/register` (customer) · ✅ `POST /auth/register-owner` (owner+negocio) — *PR #8* · ✅ `POST /auth/login` · ✅ `POST /auth/refresh` · ✅ `GET /auth/me` (protegido)
- ✅ `GET /businesses` (owner) · ✅ `GET /businesses/{id}/services` (público) · ✅ `POST /businesses/{id}/services` (owner) — *PR #6*
- ✅ `GET/PUT /businesses/{id}/hours` · ✅ `GET/POST/DELETE /businesses/{id}/holidays` (owner) — *PR #10*
- ✅ `GET /businesses/{id}/availability` (público) — *PR #11*
- ✅ `POST /reservations` · ✅ `GET /reservations/{id}` — *PR #9* · ⬜ `PATCH/DELETE /reservations/{id}`
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
| #6 | `feature/services-crud` | `services` + `ServiceService` + endpoints (owner-only create, límite Freemium) + `GET /businesses` |
| #7 | `feature/password-policy` | Política de contraseña segura en el registro (`PasswordPolicy`, 400 si débil) |
| #8 | `feature/customer-registration` | Split de registro: customer (`/auth/register`) vs owner (`/auth/register-owner`) |
| #9 | `feature/reservations-core` | `guests` + `reservations` (exclusion constraint), `CryptoService`/`BlindIndex`, `BookingService`, endpoints `POST/GET /reservations` |
| #10 | `feature/business-hours` | `business_hours` + `business_holidays` + `BusinessScheduleService` + endpoints (owner) |
| #11 | `feature/availability` | `slot_interval_minutes` + `AvailabilityService` + `GET /availability` (slots = horario − festivos − reservas); OpenAPI Bearer (Scalar Authorize); runbook Docker en SETUP.md |
| #12 | `feature/guest-user-sync` | Sync invitado→usuario: vincular guests por blind index al registrar customer (apilada sobre #11) |

> **Orden de merge pendiente:** primero PR #11 (`feature/availability`), luego PR #12 (`feature/guest-user-sync`).

---

## Siguiente paso

🎯 A elegir (dominio de reservas completo de punta a punta): **modificar/cancelar reservas** (permisos por rol — Anexo B, + hard-delete con audit), **scaffold del frontend** (React 19 + Vite), o **CI/CD** (GitHub Actions: build + test).
