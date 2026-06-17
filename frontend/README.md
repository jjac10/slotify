# Slotify Frontend

Cliente web de Slotify: **React 19 + TypeScript (strict) + Vite**. Consume la API
de ASP.NET Core del backend.

## Stack

- **Framework:** React 19
- **Lenguaje:** TypeScript 5 (modo `strict`)
- **Build/dev server:** Vite 6
- **Routing:** React Router v6
- **HTTP:** axios (cliente tipado con interceptor JWT)
- **E2E:** Playwright (contra el stack real)

## Estructura

```
frontend/
├── src/
│   ├── components/     # Layout, ProtectedRoute
│   ├── pages/          # Login, Register, ReserveFlow, MyReservations, OwnerAgenda
│   ├── hooks/          # useAuth (AuthProvider + contexto de sesión)
│   ├── services/       # apiClient (axios) + servicios por dominio + tokenStorage
│   ├── types/          # tipos del contrato de la API
│   ├── App.tsx         # rutas
│   └── main.tsx        # entrypoint (Router + AuthProvider)
├── tests/e2e/          # specs de Playwright
├── vite.config.ts      # proxy /api -> backend (dev)
├── playwright.config.ts
└── tsconfig*.json
```

## Requisitos

- **Node.js ≥ 20** (probado con 22) y npm.
- **Backend en marcha** para que la app y los e2e funcionen (ver más abajo).

## Cómo funciona la conexión con la API (sin CORS en dev)

El navegador **siempre** llama a rutas relativas `/api/...`. En desarrollo, el
dev-server de Vite hace de proxy y reenvía `/api` al backend; en producción lo
hace nginx (`infra/nginx.conf`). Así el front habla siempre con su mismo origen
y no hay problemas de CORS.

El destino del proxy en dev se configura con `VITE_API_URL` (ver
`.env.development`, por defecto `http://localhost:5000`).

## Ejecutar en local

1. Levanta el backend (desde la raíz del repo):

   ```bash
   docker compose up -d backend
   # API en http://localhost:5000 (Scalar en /scalar)
   ```

2. Instala dependencias y arranca el front:

   ```bash
   cd frontend
   npm install
   npm run dev          # http://localhost:5173
   ```

## Tests E2E (Playwright)

Corren contra el backend real, así que necesita el stack levantado. Playwright
arranca el dev-server de Vite por su cuenta.

```bash
# 1) backend arriba (docker compose up -d backend)
# 2) primera vez: instalar navegadores de Playwright
npx playwright install chromium

npm run test:e2e       # ejecuta los specs de tests/e2e
npm run test:e2e:ui    # modo interactivo
```

Primer hito cubierto: **registro → logout → login → "mis reservas" vacío**, más
login con credenciales inválidas.

## Scripts

| Script | Descripción |
| --- | --- |
| `npm run dev` | Dev server con HMR en `:5173` |
| `npm run build` | Typecheck (`tsc -b`) + build de producción |
| `npm run preview` | Sirve el build de producción |
| `npm run typecheck` | Solo comprobación de tipos |
| `npm run test:e2e` | Tests e2e de Playwright |

## Notas sobre el contrato de la API

Los tipos en `src/types/api.ts` reflejan los **DTOs reales** del backend
(serializados en camelCase), no la versión aspiracional de `docs/API.md`.
Detalles a tener en cuenta:

- `login`/`register`/`register-owner` devuelven `AuthResult`
  (`{ userId, businessId, accessToken, refreshToken }`). Se considera **owner**
  si `businessId` no es nulo.
- Crear una reserva (`POST /reservations`) exige `staffId`. Aún no existe
  endpoint público para descubrir negocios ni sus trabajadores, por eso
  `ReserveFlow` es de momento un shell (lista servicios por `businessId`).
