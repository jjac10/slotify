# Roadmap & Checklist — Slotify

> Planificación viva del proyecto. Marca dónde estamos y qué falta.
> Esquema canónico de BD: [`DATA_MODEL.md`](./DATA_MODEL.md) · Decisiones: [`DECISIONS.md`](./DECISIONS.md)

**Leyenda:** ✅ hecho · 🚧 en curso · ⬜ pendiente · 🔮 futuro (fuera de MVP)

---

## Estado actual

- **Fase activa:** 3 (Desarrollo incremental TDD).
- **Tests:** 274/274 backend en verde (xUnit + Moq + Testcontainers PostgreSQL 17 + WebApplicationFactory) + 6 e2e frontend (Playwright: auth, reserva completa, alta de servicio, horario, panel owner).
- **Lo que ya funciona (probado):** auth completa (login devuelve `businessId` del owner), negocios + servicios (CRUD con límite Freemium), **núcleo de reservas** (invitado cifrado o usuario, anti-doble-booking robusto), **horario del negocio** (horarios + festivos), **disponibilidad** (`GET /availability` con slots = horario − festivos − reservas, paso configurable) y **panel del owner** (`GET /dashboard`: contadores + ingresos del mes + próximas reservas). Flujo de reserva completo de punta a punta.
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
  - ✅ CI/CD GitHub Actions (build + test en cada push/PR: backend + frontend + e2e) — *PR #20*
  - ✅ Scaffold frontend (Vite + React 19 + TS strict) — *PR #16*
- 🚧 **Fase 3 — Desarrollo TDD** (ver detalle abajo)
- 🔮 **Fase 4 — Producción:** despliegue Ionos, HTTPS, dominio.

---

## Capa de datos (entidades + migraciones)

Comparado con [`DATA_MODEL.md`](./DATA_MODEL.md):

- ✅ `pricing_tiers` (+ seed free/premium) — *PR #1*
- ✅ `users` (mínimo: identidad, type, status) — *PR #1*
- ✅ `businesses` (mínimo: owner, tier, name, status) — *PR #1* · ✅ `timezone` — *PR #—* · ✅ `confirmation_mode` (`auto`|`manual`) — *PR #26* · ✅ cambio de plan (`tier_id` editable, upgrade/downgrade owner) — *PR #30*
  - ⬜ columnas restantes (contacto, ubicación, personalización, config, social, stats)
  - ⬜ **ubicación (lat/lng) + categoría + rating + foto** → habilita "negocios más cercanos", filtro por categoría y tarjetas ricas en Explorar/Mi Slotify (aplazado a propósito)
- ✅ `staff` (+ owner-as-staff) — *PR #2* · ✅ gestión CRUD de empleados (owner) — *PR #29*
- ✅ `services` — *PR #6*
- ✅ `staff_services` (N:M trabajador↔servicio, unique (staff_id, service_id), migración `Add_StaffServices`) — *PR #31*
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
  - ✅ `CanAddReservationThisMonthAsync` (límite reservas/mes, `IReservationRepository.CountByBusinessAsync`) · ⬜ `CanAddClientAsync`
- ✅ `ServiceService` (alta owner-only + límite, listado) — *PR #6*; `BusinessService.ListByOwnerAsync`
- ✅ **Plan/tier del negocio** (`BusinessService.ChangePlanAsync`, owner-only): cambia `tier_id` por código ('free'|'premium') → el upgrade desbloquea los límites Freemium (p. ej. añadir empleados); plan expuesto en `BusinessResponse.Plan`. En el TFM es un upgrade **simulado** (sin pago); el mismo método lo llamará el webhook de la pasarela en producción — *PR #30*. ⚠️ TODO: gatear tras pago real (Stripe/Paddle) + tabla `subscriptions`/`payments`
- ✅ `StaffService` (listado público de trabajadores activos de un negocio) — *PR #17*; `IStaffRepository.ListByBusinessAsync` · ✅ **gestión de empleados** (owner): `CreateAsync` (límite Freemium `CanAddStaffAsync` → Premium para añadir, Free solo tiene al owner), `UpdateAsync` (nombre/contacto), `DeactivateAsync` (baja lógica `status='inactive'`, el owner-staff no se puede dar de baja); `CountByBusinessAsync` cuenta solo activos (la baja libera hueco) — *PR #29*
- ✅ **`StaffServiceAssignmentService`** (owner): fija/lista qué servicios puede realizar cada trabajador (valida pertenencia al negocio); `StaffService.ListAsync(serviceId?)` filtra el listado público de staff por servicio — un trabajador sin asignaciones se ofrece para todos los servicios (compat. owner-as-staff y negocios de 1 trabajador) — *PR #31*
- ✅ `DashboardService` (resumen owner-only: contadores histórico/mes, ingresos del mes, próximas reservas) — *PR #19*; `IReservationRepository.{CountByBusiness,SumRevenueByBusiness,ListUpcomingByBusiness}Async`
- ✅ Auth: registro (bcrypt + **política de contraseña segura** *PR #7*), login (JWT HS256), refresh con rotación — *PR #5*; ✅ al registrar owner se siembra su **horario semanal por defecto** (L–V 09:00–17:00, fin de semana cerrado) en la misma transacción → disponibilidad desde el primer momento
  - ✅ `IPasswordHasher`/bcrypt, `ITokenService`/JWT, `AuthService`, `PasswordPolicy`, repos EF (`AuthRepository`, `RefreshTokenRepository`)
  - ⬜ reset password (password_reset_tokens)
- ✅ `BookingService` (crear guest/user, endTime, dedupe, overlap) + `CryptoService`/`BlindIndex` — *PR #9*
- ✅ `BusinessScheduleService` (horario semanal + festivos, owner-only, validación) — *PR #10*
- ✅ `AvailabilityService` (slots = horario − festivos − reservas, paso configurable) — *PR #11* · ⬜ timezone por negocio, anti-huecos avanzado
- ✅ `CanAddReservationThisMonthAsync` (límite Freemium de reservas) — `BookingService` lanza `FreemiumLimitReachedException` → `409 limit_reached`
- ✅ Reservas: crear con anti-doble-booking — *PR #9* · ✅ cancelar (`ReservationManagementService`: autz por rol + hard-delete + audit) — *PR #13* · ✅ reprogramar (`RescheduleAsync`: autz por rol + solape excluyéndose + optimistic locking `version` + audit `updated`) — *PR #14* · ✅ listar (agenda owner/staff + "mis reservas") — *PR #15*
- ✅ **Confirmación de reservas** (modo `auto`|`manual` por negocio): `BookingService` fija `confirmed`/`pending` según `Business.ConfirmationMode`; `ReservationManagementService.ConfirmAsync` (owner/staff, NO el cliente; `pending`→`confirmed`, optimistic locking + audit `confirmed`); `BusinessService.SetConfirmationModeAsync` (owner-only) — *PR #26*
- ✅ **Acciones de invitado + ventana de antelación**: invitado (sin login) cancela/reprograma su reserva verificándose por contacto (blind index) — `CancelAsGuestAsync`/`RescheduleAsGuestAsync`; `Business.CancellationCutoffHours` (0=sin límite): el cliente no puede cancelar/reprogramar dentro de esa ventana previa al inicio (owner/staff sí) → `409 window_closed`; `BusinessService.SetCancellationCutoffAsync` (owner) — *PR #27b*
- ✅ Guests: cifrado + blind index — *PR #9* · ✅ sync invitado→usuario automática (al registrarse, por blind index) — *PR #12* · ✅ ver reservas de invitado por teléfono/email (`GET /reservations/lookup`, blind index)
  - ⚠️ **TODO (seguridad):** el lookup de invitado debe **verificar identidad** (código por SMS al teléfono / email al correo) antes de mostrar las reservas; ahora basta con conocer el contacto. Necesario antes de producción
- ⬜ Notificaciones (async fire & forget), reviews, waitlist

---

## API (host + endpoints)

- ✅ `Slotify.API`: `Program.cs`, DI (DbContext + repos + servicios), OpenAPI/Scalar, JWT, migraciones al arranque
- ✅ **CORS** habilitado para el frontend (orígenes en `Cors:AllowedOrigins`) — *PR #15*
- ✅ `POST /auth/register` (customer) · ✅ `POST /auth/register-owner` (owner+negocio) — *PR #8* · ✅ `POST /auth/login` (devuelve `businessId` del owner — *PR #19*) · ✅ `POST /auth/refresh` · ✅ `GET /auth/me` (protegido)
- ✅ `GET /businesses` (owner; incluye `plan`) · ✅ `PUT /businesses/{id}/plan` (cambiar plan free/premium, owner) — *PR #30* · ✅ `GET /businesses/{id}/services` (público) · ✅ `POST /businesses/{id}/services` (owner) — *PR #6*
- ✅ `GET /businesses/{id}/staff` (público: elegir con quién reservar) — *PR #17* · ✅ `POST` (alta, owner+Freemium) · ✅ `PATCH /{staffId}` (editar) · ✅ `DELETE /{staffId}` (baja lógica; 409 si es el owner) — *PR #29* · ✅ `GET`/`PUT /{staffId}/services` (servicios que hace; owner) · ✅ `GET /staff?serviceId=` (filtra staff por servicio) — *PR #31*
- ✅ `GET/PUT /businesses/{id}/hours` · ✅ `GET/POST/DELETE /businesses/{id}/holidays` (owner) — *PR #10*
- ✅ `GET /businesses/{id}/availability` (público) — *PR #11*
- ✅ `POST /reservations` · ✅ `GET /reservations/{id}` — *PR #9* · ✅ `DELETE /reservations/{id}` (cancelar) — *PR #13* · ✅ `PATCH /reservations/{id}` (reprogramar) — *PR #14* · ✅ `POST /reservations/{id}/confirm` (confirmar, owner/staff) · ✅ `PUT /businesses/{id}/confirmation-mode` (auto/manual, owner) — *PR #26*
- ✅ `GET /reservations/mine` ("mis reservas") · ✅ `GET /businesses/{id}/reservations` (agenda owner/staff, filtros fecha/staff) — *PR #15*
- ✅ `GET /businesses/{id}/dashboard` (resumen owner: contadores + ingresos del mes + próximas) — *PR #19*
- ⬜ rate limiting · ⬜ manejo de errores estándar (middleware)

---

## Frontend

- ✅ Scaffold Vite + React 19 + TS (strict) · ✅ cliente API tipado (axios, interceptor JWT) — *PR #16*
- ✅ Auth (login/registro cliente+owner, JWT en localStorage, rutas protegidas) — *PR #16*
- ✅ "Mis reservas" (listado) · ✅ agenda owner (esqueleto) — *PR #16*
- ✅ Flujo de reserva completo: negocio → servicio → **staff** → fecha → slots → crear reserva (wizard de 7 pasos) — *PR #18*
- ✅ Dashboard owner (panel: contadores + ingresos + próximas) — *PR #19* · ⬜ PWA + responsive
- ✅ Gestión del negocio (owner): ver negocio (nombre + id) + **crear/listar servicios** — *PR #21* · **configurar horario semanal** (editor) — *PR #22*
- ✅ **Rediseño visual**: sistema de diseño (marca morado/cyan), logo Clock & Slot, header responsive con estados activos, status pills, cards — *PR #24* · ⬜ PWA
- ✅ **Cancelar + reprogramar reservas** en "Mis reservas" (cliente) y Agenda (owner): botón cancelar con confirmación inline + modal `RescheduleModal` con selector de fecha y slots en tiempo real — *PR #25*
- ✅ **Hub de configuración** `/configuracion` (Datos · Servicios · **Equipo** · Horario · Festivos · Confirmación · Ventana de cancelación · Plan): gestión de empleados (alta/baja, aviso Premium en Free), toggle de confirmación, ventana de cancelación — *PR settings/team*
- ✅ **`staff_services` (UI)**: asignar servicios a cada trabajador en "Equipo" (editor de chips, vacío = todos); el wizard de reserva filtra el paso de trabajador por el servicio elegido (`GET /staff?serviceId=`) — *PR staff-services-ui*
- ✅ E2E Playwright (registro+login+vacío — *PR #16*; reserva completa — *PR #18*; panel owner — *PR #19*; alta de servicio — *PR #21*; horario — *PR #22*; cancelar+reprogramar — *PR #25*) · ⬜ Vitest + RTL

---

## Infra / Calidad

- ✅ CI/CD GitHub Actions (build + test) — *PR #20*: 3 jobs (backend xUnit+Testcontainers, frontend typecheck+build, e2e Playwright vía docker compose); corre en push/PR a `main`/`develop`
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
| #18 | `feature/complete-booking-flow` | **Frontend**: flujo de reserva completo como wizard de 7 pasos (negocio → servicio → staff → fecha → slots → datos invitado → confirmado); usuario autenticado reserva en un clic. E2e Playwright de reserva completa de punta a punta |
| #19 | `feature/owner-dashboard` | `GET /businesses/{id}/dashboard` (owner-only): `DashboardService` + `DashboardResponse` + 3 agregados en `IReservationRepository` (count con ventana, ingresos vía join con services, próximas). **Fix**: `login`/`refresh` devuelven el `businessId` del owner (antes solo el registro) → el front muestra Panel/Agenda tras un login. Pantalla **Panel** en el front + e2e |
| #20 | `infra/ci-github-actions` | **CI/CD GitHub Actions**: 3 jobs en push/PR a `main`/`develop` (backend `dotnet build`+`test`; frontend typecheck+build; e2e Playwright vía docker compose). Badge de CI en el README |
| #21 | `feature/owner-business-services-ui` | **Frontend**: gestión del negocio (owner). Pantalla **Mi negocio** (nombre + id, enlace a reservar, listar/crear servicios vía `POST /businesses/{id}/services`). Estilos base (cards). E2e de alta de servicio |
| #22 | `feature/owner-business-hours` | **Frontend**: pantalla **Horario** (owner): editor del horario semanal vía `GET/PUT /businesses/{id}/hours` (toggle abierto/cerrado + apertura/cierre por día; prefija L–V 09–17). E2e de guardado de horario |
| #23 | `feature/freemium-reservation-limit` | Límite Freemium de reservas/mes: `IFreemiumLimitService.CanAddReservationThisMonthAsync` (reutiliza `CountByBusinessAsync`, ventana del mes UTC); `BookingService` lanza `FreemiumLimitReachedException` → `ReservationsController` mapea a `409 limit_reached`. Sin migración. TDD unit + integración |
| #24 | `feature/visual-redesign` | **Frontend**: rediseño visual. Sistema de diseño (tokens de marca morado/cyan, tipografía, componentes) + logo **Clock & Slot**, header con estados activos + responsive, cards de auth, métricas del panel, status pills, listas como cards, wizard pulido. Sin tocar `data-testid` (e2e intactos) |
| #25 | `feature/cancel-reschedule-ui` | **Frontend**: cancelar + reprogramar reservas. Botón "Cancelar" con confirmación inline (status pill → cancelled + desaparece) en "Mis reservas" y Agenda del owner; botón "Reprogramar" abre `RescheduleModal` (selector fecha + slots en tiempo real vía `GET /availability`). Solo para reservas activas futuras. E2e de cancelar y reprogramar |
| #26 | `feature/reservation-confirmation` | **Backend (TDD)**: confirmación de reservas. `Business.confirmation_mode` (`auto`\|`manual`, migración `Add_BusinessConfirmationMode`, default `auto`); `BookingService` fija el estado inicial (`confirmed`/`pending`) según el modo; `ReservationManagementService.ConfirmAsync` (owner/staff, NO el cliente; `pending`→`confirmed` + optimistic locking + audit `confirmed`); `BusinessService.SetConfirmationModeAsync` (owner-only); endpoints `POST /reservations/{id}/confirm` + `PUT /businesses/{id}/confirmation-mode`; `confirmation_mode` en `BusinessResponse`. 212 tests verde (+27) |
| #27 | `feature/reservation-policy` | **Backend (TDD)**: acciones de invitado + ventana de antelación. `CancelAsGuestAsync`/`RescheduleAsGuestAsync` (verificación por contacto vía blind index, `[AllowAnonymous]`); `Business.cancellation_cutoff_hours` (0=sin límite) → cliente bloqueado dentro de la ventana (owner/staff exentos) → `409 window_closed`; `BusinessService.SetCancellationCutoffAsync`. ⚠️ TODO seguridad: OTP por SMS/email antes de producción |
| #28 | `feature/settings-hub-and-guest-actions` | **Frontend**: hub de configuración (`/configuracion`) con Datos · Servicios · Horario · Festivos · Confirmación (toggle segmentado) · Ventana de cancelación; cancelar/reprogramar como invitado en "Mis reservas"; confirmar en Agenda. Rutas viejas → `/configuracion` |
| #29 | `feature/staff-management` | **Backend (TDD)**: gestión de empleados (owner). `StaffService.CreateAsync` (límite Freemium → Premium para añadir), `UpdateAsync`, `DeactivateAsync` (baja lógica; el owner-staff no se puede dar de baja); `CountByBusinessAsync` solo activos; endpoints `POST`/`PATCH`/`DELETE /businesses/{id}/staff/{staffId}`. Sin migración (tabla `staff` ya existía). 249 tests verde (+16) |
| #30 | `feature/business-plan-upgrade` | **Backend (TDD)**: plan/tier del negocio. `BusinessService.ChangePlanAsync` (owner-only) cambia `tier_id` por código ('free'\|'premium') → desbloquea límites Freemium; `BusinessResponse.Plan` (código del tier, vía `Include(Tier)`); endpoint `PUT /businesses/{id}/plan`. Upgrade **simulado** (sin pago); el mismo método lo llamará el webhook de la pasarela. Sin migración. 259 tests verde (+10). ⚠️ TODO: gatear tras pago real (Stripe) |
| #31 | `feature/staff-services` | **Backend (TDD)**: relación N:M trabajador↔servicio. Entidad `StaffServiceAssignment` + tabla `staff_services` (migración `Add_StaffServices`, unique (staff_id, service_id)); `StaffServiceAssignmentService` (owner: fija/lista qué servicios hace cada staff, valida pertenencia al negocio); `StaffService.ListAsync(serviceId?)` filtra staff por servicio (staff sin asignaciones = hace todos); endpoints `GET`/`PUT /businesses/{id}/staff/{staffId}/services` + `GET /staff?serviceId=`. 274 tests verde (+15) |

---

## Siguiente paso

🎯 Backend y frontend de empleados, plan y `staff_services` ya están (incluido el frontend de Plan y el de asignación de servicios). Siguientes candidatos:
1. **Plan solo-calendario** (ver Mejoras futuras): tipo de negocio en el que el cliente NO reserva online; solo ve el negocio y llama, y el owner apunta la reserva en su calendario/agenda manualmente.
2. **Notificaciones** (email/SMS de confirmación) y **OTP** para acciones de invitado (TODO seguridad de *PR #27*).
3. **Business profile** (categoría/ubicación) → "negocios cercanos" y filtro por categoría en Explorar.

Otras: pago real (Stripe) para el upgrade, RLS PostgreSQL, Vitest + RTL.

---

## Mejoras futuras (post-MVP)

- 🔮 **WebSockets para confirmaciones en tiempo real:** cuando el owner confirma una reserva desde Agenda, los clientes ven refrescada automáticamente su lista de reservas sin recargar. Implementable con SignalR o Socket.io.
- 🔮 **Plan "solo calendario" (owner):** un tipo de negocio que NO acepta reservas online. El cliente ve la ficha del negocio y llama por teléfono; el owner apunta la reserva manualmente en su agenda. Requiere un `booking_mode` (`online` | `calendar_only`) en `businesses` y ocultar el flujo de reserva público para esos negocios.
- 🔮 **Rol superadmin (alta controlada):** un único superadmin (el dueño de la plataforma) da de alta a los owners y sus negocios, en vez de registro abierto. Pensado para un despliegue local/pueblo donde se quiere control total del onboarding. Requiere `role=superadmin` + panel de alta de owners; el registro público de owners se desactivaría o quedaría sujeto a aprobación.
