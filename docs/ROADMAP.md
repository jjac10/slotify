# Roadmap & Checklist — Slotify

> Planificación viva del proyecto. Marca dónde estamos y qué falta.
> Esquema canónico de BD: [`DATA_MODEL.md`](./DATA_MODEL.md) · Decisiones: [`DECISIONS.md`](./DECISIONS.md)

**Leyenda:** ✅ hecho · 🚧 en curso · ⬜ pendiente · 🔮 futuro (fuera de MVP)

---

## Estado actual

- **Fase activa:** 3 (Desarrollo incremental TDD).
- **Tests:** 162/162 backend en verde (xUnit + Moq + Testcontainers PostgreSQL 17 + WebApplicationFactory) + e2e frontend (Playwright).
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
  - ✅ Scaffold frontend (Vite + React 19 + TS strict) — *PR #16*
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
- ✅ `audit_logs` (reservation_id SET NULL: sobrevive al hard-delete) — *PR #13* · ⬜ `notification_logs` · ⬜ `waitlists` · ⬜ `reviews`
- 🔮 `payments` (esqueleto documentado, no MVP)

---

## Lógica de negocio (servicios + repositorios)

- ✅ `BusinessService.CreateAsync` → crea negocio + owner-staff atómico — *PR #2*
  - ✅ `IBusinessRepository` / `BusinessRepository` (EF)
- ✅ `FreemiumLimitService` (data-driven, ADR #9): `CanAddStaffAsync` — *PR #3*, `CanAddServiceAsync` — *PR #6*
  - ✅ `ITierRepository` / `IStaffRepository` / `IServiceRepository` (+ impl. EF)
  - ⬜ `CanAddReservationThisMonthAsync`, `CanAddClientAsync`
- ✅ `ServiceService` (alta owner-only + límite, listado) — *PR #6*; `BusinessService.ListByOwnerAsync`
- ✅ `StaffService` (listado público de trabajadores activos de un negocio) — *PR #17*; `IStaffRepository.ListByBusinessAsync`
- ✅ Auth: registro (bcrypt + **política de contraseña segura** *PR #7*), login (JWT HS256), refresh con rotación — *PR #5*
  - ✅ `IPasswordHasher`/bcrypt, `ITokenService`/JWT, `AuthService`, `PasswordPolicy`, repos EF (`AuthRepository`, `RefreshTokenRepository`)
  - ⬜ reset password (password_reset_tokens)
- ✅ `BookingService` (crear guest/user, endTime, dedupe, overlap) + `CryptoService`/`BlindIndex` — *PR #9*
- ✅ `BusinessScheduleService` (horario semanal + festivos, owner-only, validación) — *PR #10*
- ✅ `AvailabilityService` (slots = horario − festivos − reservas, paso configurable) — *PR #11* · ⬜ timezone por negocio, anti-huecos avanzado
- ⬜ `CanAddReservationThisMonthAsync` (límite Freemium de reservas)
- ✅ Reservas: crear con anti-doble-booking — *PR #9* · ✅ cancelar (`ReservationManagementService`: autz por rol + hard-delete + audit) — *PR #13* · ✅ reprogramar (`RescheduleAsync`: autz por rol + solape excluyéndose + optimistic locking `version` + audit `updated`) — *PR #14* · ✅ listar (agenda owner/staff + "mis reservas") — *PR #15*
- ✅ Guests: cifrado + blind index — *PR #9* · ✅ sync invitado→usuario automática (al registrarse, por blind index) — *PR #12*
- ⬜ Notificaciones (async fire & forget), reviews, waitlist

---

## API (host + endpoints)

- ✅ `Slotify.API`: `Program.cs`, DI (DbContext + repos + servicios), OpenAPI/Scalar, JWT, migraciones al arranque
- ✅ **CORS** habilitado para el frontend (orígenes en `Cors:AllowedOrigins`) — *PR #15*
- ✅ `POST /auth/register` (customer) · ✅ `POST /auth/register-owner` (owner+negocio) — *PR #8* · ✅ `POST /auth/login` · ✅ `POST /auth/refresh` · ✅ `GET /auth/me` (protegido)
- ✅ `GET /businesses` (owner) · ✅ `GET /businesses/{id}/services` (público) · ✅ `POST /businesses/{id}/services` (owner) — *PR #6*
- ✅ `GET /businesses/{id}/staff` (público: elegir con quién reservar) — *PR #17*
- ✅ `GET/PUT /businesses/{id}/hours` · ✅ `GET/POST/DELETE /businesses/{id}/holidays` (owner) — *PR #10*
- ✅ `GET /businesses/{id}/availability` (público) — *PR #11*
- ✅ `POST /reservations` · ✅ `GET /reservations/{id}` — *PR #9* · ✅ `DELETE /reservations/{id}` (cancelar) — *PR #13* · ✅ `PATCH /reservations/{id}` (reprogramar) — *PR #14*
- ✅ `GET /reservations/mine` ("mis reservas") · ✅ `GET /businesses/{id}/reservations` (agenda owner/staff, filtros fecha/staff) — *PR #15*
- ⬜ Dashboard owner (resumen/stats) · ⬜ rate limiting · ⬜ manejo de errores estándar (middleware)

---

## Frontend

- ✅ Scaffold Vite + React 19 + TS (strict) · ✅ cliente API tipado (axios, interceptor JWT) — *PR #16*
- ✅ Auth (login/registro cliente+owner, JWT en localStorage, rutas protegidas) — *PR #16*
- ✅ "Mis reservas" (listado) · ✅ agenda owner (esqueleto) — *PR #16*
- 🚧 Flujo de reserva: lista servicios + **staff** (*PR #17*) · ⬜ slots disponibles · ⬜ crear reserva
- ⬜ Dashboard owner · ⬜ PWA + responsive
- ✅ E2E Playwright (registro+login+mis reservas) — *PR #16* · ⬜ Vitest + RTL

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
| #12 | `feature/guest-user-sync` | Sync invitado→usuario: vincular guests por blind index al registrar customer |
| #13 | `feature/cancel-reservation` | `audit_logs` + `ReservationManagementService.CancelAsync` + `DELETE /reservations/{id}` (autz rol + audit + hard-delete) |
| #14 | `feature/modify-reservation` | `ReservationManagementService.RescheduleAsync` + `PATCH /reservations/{id}` (reprogramar: conserva duración, solape excluyéndose, optimistic locking `version`, audit `updated`); `IReservationRepository.UpdateAsync` |
| #15 | `feature/list-reservations` | Listar reservas: `GET /reservations/mine` + `GET /businesses/{id}/reservations` (agenda owner/staff, filtros fecha/staff) + `ListBy{Business,User}Async`; **CORS** para el frontend; e2e de `GET /businesses` (hueco de cobertura) |
| #16 | `feature/frontend-scaffold` | **Frontend** React 19 + Vite + TS strict: cliente API tipado (axios + interceptor JWT), auth (login/registro cliente+owner), rutas (React Router v6), "mis reservas" + agenda owner, e2e Playwright (registro+login+vacío) contra el stack real |
| #17 | `feature/staff-listing` | `GET /businesses/{id}/staff` (público): `StaffResponse` + `StaffService` + `IStaffRepository.ListByBusinessAsync` (activos, ordenados por nombre). Desbloquea el `staffId` del flujo de reserva |

---

## Siguiente paso

🎯 **Completar el flujo de reserva en el frontend** (con `GET /businesses/{id}/staff` ya disponible): encadenar negocio → servicio → staff → fecha → slots (`GET /availability`) → `POST /reservations`. Alternativas: **CI/CD** (GitHub Actions build+test) o **límite Freemium de reservas/mes** (`CanAddReservationThisMonthAsync`).
