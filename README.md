# Slotify

[![CI](https://github.com/jjac10/slotify/actions/workflows/ci.yml/badge.svg)](https://github.com/jjac10/slotify/actions/workflows/ci.yml)

### 🌐 Demo en vivo: **[slotify.jjalarcon.es](https://slotify.jjalarcon.es)**

> Desplegada en un VPS (Ionos) con HTTPS automático. Cada `merge` a `main` reconstruye, prueba
> y **redespliega solo** (GitHub Actions → imágenes en GHCR → SSH al VPS → Traefik). Detalles en [docs/DEPLOY.md](docs/DEPLOY.md).

**Slotify** es una plataforma de reservas para negocios locales (estilo Booksy): el cliente
descubre negocios, reserva en segundos (con cuenta o como invitado) y el dueño gestiona su
agenda, equipo, horarios, precios y avisos. Proyecto **TFM** desarrollado con metodología
**TDD estricta** asistida por IA.

## Stack
- **Backend:** ASP.NET Core 10 + Entity Framework Core 10 + PostgreSQL 17 (Repository Pattern + DI, BD intercambiable)
- **Frontend:** React 19 + TypeScript + Vite + Tailwind CSS
- **Testing:** xUnit + Moq + Testcontainers (backend) · Playwright (frontend e2e)
- **Infra:** Docker + Docker Compose + GitHub Actions (CI: build + test + e2e · CD: build→GHCR→deploy al VPS)
- **Producción:** VPS Ionos + Traefik (HTTPS Let's Encrypt) — auto-deploy en cada `merge` a `main` (ver [docs/DEPLOY.md](docs/DEPLOY.md))
- **API docs:** OpenAPI nativa + Scalar en `/scalar`

## Funcionalidades

**Cliente**
- Registro/login (JWT con refresh) y reserva **como invitado** (teléfono 🇪🇸 +34 o email, cifrados AES-256-GCM)
- **Explorar** negocios: búsqueda por nombre, filtro por categoría, "cerca de mí" (distancia), valoración media
- **Reserva de punta a punta**: servicio → profesional → día → hueco disponible (horario − festivos − reservas)
- **Mis reservas**: próximas/pasadas, cancelar y reprogramar (respetando la ventana de antelación del negocio)
- Al registrarte con tu teléfono, **se vinculan** las reservas que hiciste antes como invitado
- **Reseñas**: una por negocio (tras asistir), editable; pantalla **"Mis reseñas"**

**Dueño**
- **Panel** con métricas (reservas del mes, ingresos estimados, valoración) y reseñas recientes
- **Agenda** del negocio: vista **lista** (próximas/pasadas, agrupada por día, filtros) y vista **día tipo calendario** (timeline)
- **Reserva manual** desde la agenda (recepción)
- Gestión de **servicios**, **equipo** (con asignación de servicios por trabajador), **horario** semanal y **festivos**
- **Modo de reservas**: online (sale en Explorar) o **solo calendario** (agenda privada, "cita en persona")
- **Confirmación** automática/manual y **ventana de cancelación** configurables
- **Avisos a clientes** (creada/confirmada/reprogramada/cancelada + recordatorio) por email/WhatsApp — *envío simulado y registrado en esta versión; el `INotificationSender` es intercambiable por un proveedor real*
- **Plan** Free/Premium (límites Freemium; upgrade simulado)

**Transversal**
- Anti-doble-booking robusto (constraint de exclusión en BD), optimistic locking, auditoría de cambios
- Datos personales del invitado cifrados y nunca en la URL (van en el body)

## Puesta en marcha

Requisitos: **Docker Desktop**, **.NET 10 SDK**, **Node 22+**.

```bash
git clone https://github.com/jjac10/slotify.git
cd slotify

# Backend + base de datos
docker compose up -d --build backend     # API en http://localhost:5000 (Scalar en /scalar)

# Frontend (dev server con proxy /api → :5000)
cd frontend && npm install && npm run dev # http://localhost:5173
```

## Datos de prueba (demo)

Con la API en marcha, siembra un negocio demo (servicios, horario, reservas y una reseña)
más un cliente, con un único comando:

```bash
node scripts/seed-demo.mjs
```

Credenciales que crea (contraseña común **`Demo1234!`**):

| Rol     | Email                  | Contraseña  |
|---------|------------------------|-------------|
| Dueño   | `owner@demo.slotify`   | `Demo1234!` |
| Cliente | `cliente@demo.slotify` | `Demo1234!` |

> Pensado para una BD limpia. El negocio demo ("Barbería Demo") aparece en **Explorar** con
> 3 servicios, horario L–V, una reserva pasada, dos futuras y una reseña de 5★.

## Tests

```bash
cd backend && dotnet test        # unit + integración (Testcontainers → necesita Docker en marcha)
cd frontend && npx playwright test
```

**300+ pruebas de backend** (unit + integración) y **16 specs e2e** Playwright; el pipeline de
**CI** ejecuta build + test (backend) + typecheck/build + e2e (frontend) en cada push/PR.

## Estructura

```
slotify/
├── backend/      # ASP.NET Core: Slotify.Domain · Slotify.Infrastructure · Slotify.API · Slotify.Tests
├── frontend/     # React 19 + TS + Vite (src/pages, components, services) + tests/e2e
├── infra/        # Dockerfiles (backend/frontend) + nginx
├── scripts/      # seed-demo.mjs (datos de prueba)
├── docs/         # documentación (índice abajo)
└── docker-compose.yml
```

## Arquitectura (resumen)
- **Repository Pattern + DI** → la base de datos es intercambiable sin tocar la lógica de dominio
- **Code First + EF Core Migrations** versionadas en git
- **Cifrado** AES-256-GCM (contacto del invitado) + blind index HMAC para búsqueda/unicidad
- **Zonas horarias**: UTC en BD, local en UI

## Documentación

- [ROADMAP](docs/ROADMAP.md) — planificación y checklist
- [REQUIREMENTS](docs/REQUIREMENTS.md) — requisitos y casos de uso
- [DATA_MODEL](docs/DATA_MODEL.md) — esquema canónico de BD
- [DECISIONS](docs/DECISIONS.md) — ADRs (decisiones de arquitectura)
- [ARCHITECTURE](docs/ARCHITECTURE.md) · [API](docs/API.md) · [DATABASE](docs/DATABASE.md)
- [SETUP](docs/SETUP.md) · [DEVELOPMENT](docs/DEVELOPMENT.md) · [GITFLOW](GITFLOW.md)
