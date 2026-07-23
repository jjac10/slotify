# Roadmap & Checklist — Slotify

> Planificación viva del proyecto. Marca dónde estamos y qué falta.
> Esquema canónico de BD: [`DATA_MODEL.md`](./DATA_MODEL.md) · Decisiones: [`DECISIONS.md`](./DECISIONS.md)

**Leyenda:** ✅ hecho · 🚧 en curso · ⬜ pendiente · 🔮 futuro (fuera de MVP)

---

## Estado actual

- **🚀 EN PRODUCCIÓN:** **https://slotify.jjalarcon.es** (release **v1.0.0**, 2026-06-26). HTTPS Let's Encrypt vía Traefik; auto-deploy en cada `merge` a `main`. Ver [`DEPLOY.md`](./DEPLOY.md).
- **Fase activa:** 4 (Producción) — desplegado. Quedan entregables del TFM: memoria, slides y vídeo.

- **Tests:** 294/294 backend en verde (xUnit + Moq + Testcontainers PostgreSQL 17 + WebApplicationFactory) + 6 e2e frontend (Playwright: auth, reserva completa, alta de servicio, horario, panel owner).
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
- ✅ **Fase 4 — Producción:** desplegado en VPS Ionos con dominio `slotify.jjalarcon.es` y HTTPS (Let's Encrypt).
  - ✅ Imágenes a **GHCR** desde CI (`deploy.yml` por `workflow_run` tras CI verde en `main`)
  - ✅ **`docker-compose.prod.yml`** (postgres + backend + frontend) enchufado al **Traefik** existente del VPS (red `traefik_net`, certresolver `letsencrypt`, entrypoint `websecure`)
  - ✅ **Auto-deploy**: `merge` a `main` → build+push → SSH al VPS → `docker compose pull && up -d`
  - ✅ Release **v1.0.0** etiquetada · secretos en `/opt/slotify/.env` (fuera del repo)

---

## Capa de datos (entidades + migraciones)

Comparado con [`DATA_MODEL.md`](./DATA_MODEL.md):

- ✅ `pricing_tiers` (+ seed free/premium) — *PR #1*
- ✅ `users` (mínimo: identidad, type, status) — *PR #1*
- ✅ `businesses` (mínimo: owner, tier, name, status) — *PR #1* · ✅ `timezone` — *PR #—* · ✅ `confirmation_mode` (`auto`|`manual`) — *PR #26* · ✅ cambio de plan (`tier_id` editable, upgrade/downgrade owner) — *PR #30*
  - ⬜ columnas restantes (contacto, personalización, config, social, stats)
  - ✅ **perfil público**: `category`, `photo_url`, `latitude`, `longitude` (migración `Add_BusinessProfile`) → tarjetas con foto, filtro por categoría y "negocios cercanos" (distancia) en Explorar — *PR business-profile* · ⬜ `rating` (pendiente del sistema de reseñas)
- ✅ `staff` (+ owner-as-staff) — *PR #2* · ✅ gestión CRUD de empleados (owner) — *PR #29*
- ✅ `services` — *PR #6*
- ✅ `staff_services` (N:M trabajador↔servicio, unique (staff_id, service_id), migración `Add_StaffServices`) — *PR #31*
- ✅ `guests` (AES-256-GCM + HMAC blind index) — *PR #9*
- ✅ `reservations` (+ exclusion constraint gist anti-doble-booking, optimistic locking) — *PR #9*
- ✅ `business_hours` · ✅ `business_holidays` (+ `end_date` rango de días, `start_time`/`end_time` cierre parcial — migración `Add_HolidayRangeAndHours`) — *PR #10 / holiday-ranges*
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
- ✅ `ServiceService` (alta owner-only + límite, listado) — *PR #6*; `BusinessService.ListByOwnerAsync` · ✅ **editar/eliminar servicios** (owner): `UpdateAsync` + `DeleteAsync` (archivado lógico `status='archived'` para conservar el histórico de reservas; libera hueco del plan)
- ✅ **Plan/tier del negocio** (`BusinessService.ChangePlanAsync`, owner-only): cambia `tier_id` por código ('free'|'premium') → el upgrade desbloquea los límites Freemium (p. ej. añadir empleados); plan expuesto en `BusinessResponse.Plan`. En el TFM es un upgrade **simulado** (sin pago); el mismo método lo llamará el webhook de la pasarela en producción — *PR #30*. ✅ TODO cerrado en la rama v2: el upgrade está **gateado tras el pago** (`SubscriptionService` + checkout de Stripe real o simulado + tabla `subscriptions`; `PUT /plan premium` → 409 `payment_required`)
- ✅ `StaffService` (listado público de trabajadores activos de un negocio) — *PR #17*; `IStaffRepository.ListByBusinessAsync` · ✅ **gestión de empleados** (owner): `CreateAsync` (límite Freemium `CanAddStaffAsync` → Premium para añadir, Free solo tiene al owner), `UpdateAsync` (nombre/contacto), `DeactivateAsync` (baja lógica `status='inactive'`, el owner-staff no se puede dar de baja); `CountByBusinessAsync` cuenta solo activos (la baja libera hueco) — *PR #29*
- ✅ **`StaffServiceAssignmentService`** (owner): fija/lista qué servicios puede realizar cada trabajador (valida pertenencia al negocio); `StaffService.ListAsync(serviceId?)` filtra el listado público de staff por servicio — un trabajador sin asignaciones se ofrece para todos los servicios (compat. owner-as-staff y negocios de 1 trabajador) — *PR #31*
- ✅ `DashboardService` (resumen owner-only: contadores histórico/mes, ingresos del mes, próximas reservas) — *PR #19*; `IReservationRepository.{CountByBusiness,SumRevenueByBusiness,ListUpcomingByBusiness}Async`
- ✅ Auth: registro (bcrypt + **política de contraseña segura** *PR #7*), login (JWT HS256), refresh con rotación — *PR #5*; ✅ al registrar owner se siembra su **horario semanal por defecto** (L–V 09:00–17:00, fin de semana cerrado) en la misma transacción → disponibilidad desde el primer momento
  - ✅ `IPasswordHasher`/bcrypt, `ITokenService`/JWT, `AuthService`, `PasswordPolicy`, repos EF (`AuthRepository`, `RefreshTokenRepository`)
  - ⬜ reset password (password_reset_tokens)
- ✅ `BookingService` (crear guest/user, endTime, dedupe, overlap) + `CryptoService`/`BlindIndex` — *PR #9* · ✅ **reserva manual del owner/staff para un cliente** (datos de invitado aunque la petición esté autenticada → no es self-booking); si el contacto (teléfono/email normalizado) coincide con una cuenta existente, la reserva se **vincula a esa cuenta** (aparece en su "Mis reservas"); si no, se crea como invitado — *PR owner-manual-booking*. ⚠️ El match por teléfono usa normalización básica (espacios); E.164 completa es mejora futura
- ✅ `BusinessScheduleService` (horario semanal + festivos, owner-only, validación) — *PR #10*
- ✅ `AvailabilityService` (slots = horario − festivos − reservas, paso configurable) — *PR #11*; ✅ los festivos pueden ser un **rango de días** y/o un **cierre parcial por horas** (resta solo esa franja de los huecos del día) — *PR holiday-ranges* · ⬜ timezone por negocio
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
- ✅ `GET /businesses` (owner; incluye `plan`) · ✅ `PUT /businesses/{id}/plan` (cambiar plan free/premium, owner) — *PR #30* · ✅ `GET /businesses/{id}/services` (público) · ✅ `POST` · ✅ `PUT`/`DELETE /businesses/{id}/services/{serviceId}` (editar/eliminar, owner) — *PR #6*
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
- ✅ Flujo de reserva completo: negocio → servicio → **staff** → fecha → slots → crear reserva (wizard de 7 pasos) — *PR #18*; ✅ **calendario mensual propio** (`MonthCalendar`: rejilla con navegación de meses, días pasados deshabilitados, día seleccionado resaltado) en lugar del input de fecha nativo — *PR month-calendar*
- ✅ Dashboard owner (panel: contadores + ingresos + próximas) — *PR #19* · ✅ PWA (rama v2) · ✅ pasada responsive sistemática (rama v2): auditoría a 375px sin desbordes + spec guardarraíl `responsive.spec.ts`
- ✅ Gestión del negocio (owner): ver negocio (nombre + id) + **crear/listar servicios** — *PR #21* · **configurar horario semanal** (editor) — *PR #22*
- ✅ **Rediseño visual**: sistema de diseño (marca morado/cyan), logo Clock & Slot, header responsive con estados activos, status pills, cards — *PR #24* · ✅ PWA (rama v2)
- ✅ **Cancelar + reprogramar reservas** en "Mis reservas" (cliente) y Agenda (owner): botón cancelar con confirmación inline + modal `RescheduleModal` con selector de fecha y slots en tiempo real — *PR #25*
- ✅ **Reserva manual desde la Agenda** (owner): botón "Nueva reserva" → modal `NewReservationModal` (servicio → profesional filtrado por servicio → fecha → hueco en rejilla uniforme → datos del cliente). El owner apunta reservas de clientes (recepción); base del futuro plan solo-calendario — *PR owner-manual-booking*
- ✅ **Agenda más informativa**: cada cita muestra el nombre del cliente (invitado o usuario) y una etiqueta Invitado/Cliente; `ReservationResponse.ClientName` (vía navegaciones `Guest`/`User`) — *PR agenda-and-slots-polish*
- ✅ **Hub de configuración** `/configuracion` (Datos · Servicios · **Equipo** · Horario · Festivos · Confirmación · Ventana de cancelación · Plan): gestión de empleados (alta/baja, aviso Premium en Free), toggle de confirmación, ventana de cancelación — *PR settings/team*
- ✅ **`staff_services` (UI)**: asignar servicios a cada trabajador en "Equipo" (editor de chips, **todos marcados por defecto** = realiza todos); el wizard de reserva filtra el paso de trabajador por el servicio elegido (`GET /staff?serviceId=`) — *PR staff-services-ui*
- ✅ **Editar/eliminar servicios** en la sección Servicios del hub (edición inline + borrado con confirmación) y **horario por defecto** al registrar el negocio — *PR services-crud / default-hours*
- ✅ E2E Playwright (registro+login+vacío — *PR #16*; reserva completa — *PR #18*; panel owner — *PR #19*; alta de servicio — *PR #21*; horario — *PR #22*; cancelar+reprogramar — *PR #25*) · ⬜ Vitest + RTL

---

## Infra / Calidad

- ✅ CI/CD GitHub Actions (build + test) — *PR #20*: 3 jobs (backend xUnit+Testcontainers, frontend typecheck+build, e2e Playwright vía docker compose); corre en push/PR a `main`/`develop`
- ✅ Fijar versión parcheada de `System.Security.Cryptography.Xml` (NU1903): 10.0.10, `dotnet list package --vulnerable` limpio (rama v2)
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

🎯 **MVP completo y en producción** (`slotify.jjalarcon.es`, v1.0.0). El núcleo está cerrado: auth, reservas (usuario + invitado), horarios, disponibilidad, panel, agenda (lista + calendario), empleados, plan/`staff_services`, perfil público, reseñas, modo solo-calendario y notificaciones (envío simulado).

**Entrega TFM completada** (jul 2026): slides en `docs/slides/`, vídeo enviado. Lo demás, en **🔮 Mejoras post-entrega** (abajo).

---

## Mejoras futuras (post-MVP)

- 🔮 **WebSockets para confirmaciones en tiempo real:** cuando el owner confirma una reserva desde Agenda, los clientes ven refrescada automáticamente su lista de reservas sin recargar. Implementable con SignalR o Socket.io.
- 🔮 **Plan "solo calendario" (owner):** un tipo de negocio que NO acepta reservas online. El cliente ve la ficha del negocio y llama por teléfono; el owner apunta la reserva manualmente en su agenda. Requiere un `booking_mode` (`online` | `calendar_only`) en `businesses` y ocultar el flujo de reserva público para esos negocios.
- 🔮 **Rol superadmin (alta controlada):** un único superadmin (el dueño de la plataforma) da de alta a los owners y sus negocios, en vez de registro abierto. Pensado para un despliegue local/pueblo donde se quiere control total del onboarding. Requiere `role=superadmin` + panel de alta de owners; el registro público de owners se desactivaría o quedaría sujeto a aprobación.

---

## 🔮 Mejoras post-entrega (después del TFM v1.0.0)

Backlog priorizado tras la entrega. Marca: 🟢 bajo esfuerzo · 🟡 medio · 🔴 alto.

### ✅ Hecho en la rama `v2` (jul 2026, pendiente de merge tras la revisión del TFM)

Primer bloque de "corteza de producto" para comercializar, implementado con TDD por
subagentes de Claude Code (definidos en `.claude/agents/` + skill `.claude/skills/tdd-slice`):

- ✅ **Recuperación de contraseña** (`/recuperar`, `/restablecer`) — token 256-bit un solo uso (solo hash en BD), email **simulado** vía `IAccountEmailSender` (swappable por proveedor real), anti-enumeración, revoca sesiones al resetear.
- ✅ **Verificación de email no bloqueante** — banner descartable + `/verificar-email`; el registro nunca falla por el email.
- ✅ **RGPD**: páginas legales (`/legal/terminos|privacidad|cookies`) + **borrado de cuenta** (`DELETE /auth/me`, tombstone anonimizado, transaccional, 409 si el owner tiene reservas futuras).
- ✅ **Rate limiting** anti fuerza bruta en login/register (10/60s por IP, configurable, 429 + Retry-After).
- ✅ **Paginación** del listado público de negocios (`page`/`pageSize`, envoltorio `{ items, total, page, pageSize }`, "Cargar más" en Explorar).
- ✅ **Observabilidad**: Serilog (JSON CLEF en prod, sin datos personales) + `/health` y `/health/ready` + healthcheck Docker del backend.
- ✅ **Backups automáticos**: sidecar `pg_dump` diario con retención configurable (ver `DEPLOY.md`).

Suite al cierre del primer bloque: **423 tests backend + 34 pruebas e2e (22 specs), todo en verde**.

Segundo bloque (2026-07-08):

- ✅ **Landing page pública rediseñada** según `design/slotify_public_landing_page/` (header sticky, hero radial con tarjeta flotante, features, sección QR, CTA de negocio, footer legal+contacto).
- ✅ **Paginación y filtros en listados de reservas**: `GET /reservations/mine` y agenda devuelven `PagedResponse` (`page`/`pageSize` 20/50, 400 `invalid_pagination`); filtro `scope=upcoming|past|all` en "mis reservas" (past descendente); toggle Próximas|Pasadas|Todas + "Cargar más" en el front; spec `reservations-pagination.spec.ts`.
- ✅ **Página de contacto/soporte** (`/contacto` + `POST /support/contact`, público y rate-limited): `SupportService` + `ISupportEmailSender` swappable (simulado por log), spec `contact.spec.ts`.
- ✅ **Email real por SMTP (MailKit)**: `SmtpEmailSender` cubre los tres seams (cuenta, avisos, soporte) vía `ISmtpTransport` (STARTTLS); config por `SMTP_HOST/PORT/USER/PASSWORD` del entorno con **fallback simulado** si faltan credenciales; `docker-compose.prod.yml` pasa las variables y fija `Frontend__BaseUrl` (enlaces de email correctos en prod). WhatsApp sigue simulado.
- ✅ **Vitest + React Testing Library**: `npm run test:unit` (22 tests: `MonthCalendar`, `StatusPill`, `GuestContactInput`) integrado en el job de frontend de CI.
- ✅ **WhatsApp real vía Twilio** (2026-07-09): `WhatsAppNotificationSender` + `TwilioWhatsAppTransport` (sandbox en dev); config por `TWILIO_ACCOUNT_SID/AUTH_TOKEN/WHATSAPP_FROM` con fallback simulado, encadenado con la cadena de email.
- ✅ **Calendario con disponibilidad (estilo Booksy)** (2026-07-09): `GET /businesses/{id}/availability/month` ('closed'|'full'|'almost_full'|'available' por día, una consulta de reservas por mes) + puntos verde/ámbar/rojo en `MonthCalendar` y en la tira de días del wizard; días completos/cerrados no seleccionables.

Tercer bloque (2026-07-10):

- ✅ **OTP para acciones de invitado** *(cierra el TODO de seguridad)*: código de 6 dígitos (solo hash en BD, 10 min, máx. 5 intentos, tabla `guest_otp_codes`) exigido en lookup/cancelar/reprogramar de invitado (403 `invalid_otp`); envío por email (SMTP real) o teléfono (WhatsApp/Twilio; seam `IGuestOtpSender` preparado para SMS), anti-enumeración + rate limiting; "Mis reservas" de invitado en dos pasos.
- ✅ **Paginación de reseñas** + sección de reseñas visible por fin en la ficha de Explorar ("Ver más" de 5 en 5).
- ✅ **Borrar negocio** con confirmación máxima (nombre exacto + contraseña, 409 con reservas futuras) + **admin de moderación** (`Admin:Email`/`ADMIN_EMAIL`, sin tocar el modelo): página `/admin` con directorio y borrado en cascada.
- ✅ **Solo-calendario: horario y huecos** en la ficha pública (`WeeklyHours` + huecos de hoy orientativos).
- ✅ **Personalización del perfil público**: descripción (500), web e Instagram (migración `Add_BusinessProfileCustomization`) en Configuración → Datos y en la ficha.
- ✅ **Métricas avanzadas**: marcar "No vino" en la Agenda (POST `/reservations/{id}/no-show`) + tasa de no-shows y ocupación del mes en el panel.

Suite al cierre del tercer bloque: **329 unit tests backend + 26 unit frontend en verde**; los tests de integración (Testcontainers) y e2e de los bloques 2–3 están escritos pero **pendientes de una pasada con Docker** antes del merge (Docker Desktop no disponible en las sesiones). *(Pasada completa hecha el 2026-07-14: 577 backend + 40 e2e en verde.)*

Cuarto bloque (2026-07-14 → 2026-07-19):

- ✅ **Confirmaciones en tiempo real (SignalR)**: la lista del cliente se refresca sola cuando el owner confirma la reserva.
- ✅ **Lista de espera (`waitlists`)**: sin huecos, el cliente entra en cola y se le avisa (email/WhatsApp) al liberarse un hueco.
- ✅ **Pago real para Premium** *(cierra el 🔴 de producto)*: el upgrade entra SIEMPRE por el checkout de la pasarela (`IPaymentGateway` swappable: **Stripe Checkout real** con claves `STRIPE_*` o **checkout simulado** demoable sin ellas). `POST /businesses/{id}/checkout` crea la suscripción `pending` (tabla `subscriptions`, migración `Add_Subscriptions`); la activa el webhook `checkout.session.completed` (firma `Stripe-Signature` verificada con HMAC propio + anti-replay) o el retorno simulado, idempotente. `PUT /plan premium` directo → 409 `payment_required`; el downgrade a Free cancela la suscripción. Front: "Mejorar a Premium" abre la URL de pago y vuelve a Configuración con banner de éxito (`?upgraded=1`); spec `premium-upgrade.spec.ts`.

- ✅ **PWA instalable**: manifest + service worker (`vite-plugin-pwa`), shell cacheada para el arranque y `/api` siempre por red (nada de datos obsoletos).
- ✅ **Subida de la foto del negocio** *(antes solo pegar URL)*: `POST /businesses/{id}/photo` (multipart, JPG/PNG/WebP, máx. 5 MB, solo el owner) → `IPhotoStorage` swappable con `LocalPhotoStorage` (un fichero por negocio, `?v=` cache-buster), servida por el propio backend en `/uploads` (→ `/api/uploads` tras el proxy) y persistida en `/opt/slotify/uploads` en prod (bind mount, sin configurar nada). Botón "Subir imagen" en Configuración → Perfil; spec `business-photo.spec.ts`.

- ✅ **Logo propio + color de marca** *(cierra el "pendiente de futuro" del perfil)*: `logo_url` y `brand_color` (#rrggbb validado, migración `Add_BusinessBranding`); el logo se sube como la foto (`POST /businesses/{id}/logo`, slot aparte en `IPhotoStorage`: `{id}-logo.ext` no pisa `{id}-photo.ext`) y el color se edita con un picker en Configuración → Perfil. En la ficha pública: logo junto al nombre y color en la banda superior (si no hay foto); spec `business-branding.spec.ts`.
- ✅ Parche de seguridad: `System.Security.Cryptography.Xml` fijado a 10.0.10 (NU1903 fuera; `dotnet list package --vulnerable` limpio).
- ✅ **Pasada responsive sistemática**: auditoría a 375px (landing, Explorar + ficha, login/registro, contacto, panel, agenda, Configuración con todas las secciones, mis reservas, wizard) — sin desbordes horizontales ni roturas. Queda `responsive.spec.ts` como guardarraíl (autosuficiente, corre en CI): captura las vistas y falla si alguna desborda el viewport.

Suite al cierre del cuarto bloque: **653 tests backend en verde** (unit + integración con Docker) + build del front y e2e afectados (premium-upgrade, staff-accounts, team, business-photo, business-branding) en verde.

### Producto / negocio
- ✅ **Notificaciones reales (email + WhatsApp)** (rama v2): email por SMTP (IONOS vía MailKit) y WhatsApp por Twilio (sandbox en dev), ambos con fallback simulado si faltan credenciales.
- ✅ **Borrar negocio** (rama v2): cascada RGPD con confirmación máxima (nombre exacto + contraseña).
- ✅ **Rol admin de plataforma (moderación)** (rama v2): email configurado (`ADMIN_EMAIL`) + página `/admin` con directorio y borrado en cascada. El registro sigue abierto.
- ✅ **Modo solo-calendario: horario y huecos** en la ficha pública (rama v2).
- ✅ **Pago real para Premium** (rama v2): Stripe Checkout + webhook firmado + tabla `subscriptions`; sin claves de Stripe el checkout es simulado (demo). El upgrade directo quedó gateado (409 `payment_required`).
- ✅ **Personalización del perfil público** (rama v2): descripción, web e Instagram (además de la foto/categoría/contacto que ya existían). ✅ **Subida de la foto** desde Configuración (rama v2; antes solo por URL). ✅ **Logo propio y color de marca** (rama v2): se lucen en la ficha pública.
- ✅ **Página de contacto / soporte** (rama v2): `/contacto` → email al dueño de la plataforma.

### Seguridad
- ✅ **OTP para acciones de invitado** (rama v2): código por email/WhatsApp antes de ver o gestionar reservas por contacto; el seam deja preparado el SMS real.
- 🟡 **Rotación anual de claves** (HMAC/cifrado): rotar implica recalcular hashes/blind index.

### Funcionalidad
- ✅ **Lista de espera (`waitlists`)** (rama v2): sin huecos, el cliente entra en cola y se le avisa al liberarse uno.
- ✅ **Confirmaciones en tiempo real (SignalR)** (rama v2): la lista del cliente se refresca sola cuando el owner confirma.
- ✅ **Métricas avanzadas** (rama v2): "No vino" en la Agenda + tasa de no-shows y ocupación del mes en el panel.
- ✅ **Paginación y filtros** en listados de reservas y reseñas (rama v2).

### Plataforma / rendimiento
- 🟡 **CQRS-lite / vistas materializadas** para reportes (ver `DECISIONS.md` #8).
- 🟡 **Redis** como caché cuando el rendimiento lo pida.
- 🟡 **RLS en PostgreSQL**. (Vitest + RTL ✅ en la rama v2.)
