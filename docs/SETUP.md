# Setup Local - Slotify

## Requisitos
- Docker + Docker Compose
- .NET 10 SDK (opcional si usas Docker)
- Node 22+ (opcional si usas Docker)
- Git

## Opción 1: Docker (Recomendado) — runbook actual

> ⚠️ De momento **solo se levantan `postgres` + `backend`**. El servicio `frontend`
> del compose **aún no funciona** (no hay `package.json`); no uses `docker compose up`
> a secas o fallará la build del frontend.

```powershell
# 1. Clonar y entrar
git clone https://github.com/jjac10/slotify.git
cd slotify

# 2. Levantar postgres + API (compila la imagen del backend la 1ª vez, ~2-3 min)
docker compose up --build backend        # añade -d para dejarlo en segundo plano

# (las migraciones se aplican SOLAS al arrancar; no hay que ejecutar nada)
```

**Acceso:**
- API: http://localhost:5000
- **Scalar (UI para probar la API):** http://localhost:5000/scalar
- OpenAPI JSON: http://localhost:5000/openapi/v1.json
- PostgreSQL: localhost:5432 (db `slotify` · user `slotify_user` · pass `slotify_password`)

**Probar el flujo en Scalar:**
1. `POST /auth/register-owner` → copia `accessToken` y `businessId`.
2. Pulsa **Authorize** (arriba) y pega el `accessToken` (sin `Bearer `) → se envía en todas las peticiones.
3. `POST /businesses/{id}/services` → copia el `id` del servicio.
4. Necesitas el `staffId` (el owner-staff). Míralo en la BD:
   ```powershell
   docker exec slotify_db psql -U slotify_user -d slotify -c "select id, role from staff;"
   ```
5. `PUT /businesses/{id}/hours` → fija el horario · `GET /businesses/{id}/availability` → slots · `POST /reservations`.

**Parar / reiniciar:**
```powershell
docker compose down            # para los contenedores, CONSERVA los datos (volumen)
docker compose down -v         # para y BORRA los datos (BD desde cero)
docker compose up -d backend   # vuelve a arrancar (sin reconstruir)
docker compose up -d --build backend   # reconstruye tras cambiar código del backend
docker compose logs -f backend # ver logs del API
```

## Opción 2: Local (Sin Docker)

### Backend
```bash
cd backend
dotnet restore
dotnet ef database update  # Crear BD
dotnet run
```

Backend en http://localhost:5000

### Frontend
```bash
cd frontend
npm install
npm run dev
```

Frontend en http://localhost:5173

### PostgreSQL
```bash
# Instalar PostgreSQL localmente o Docker
docker run -d \
  -e POSTGRES_DB=slotify \
  -e POSTGRES_USER=slotify_user \
  -e POSTGRES_PASSWORD=slotify_password \
  -p 5432:5432 \
  postgres:17-alpine
```

## Testing

### Backend
Los tests de integración usan **Testcontainers**, así que necesitan **Docker en marcha**.
```bash
cd backend
dotnet test          # 114/114 verde
```

### Frontend
> Pendiente de scaffold (React 19 + Vite). Cuando exista: `npm run test` / `npm run test:e2e`.

## CI/CD
> ⬜ Pendiente: GitHub Actions (build + test en cada push). Ver [ROADMAP](./ROADMAP.md).
