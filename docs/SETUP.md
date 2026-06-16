# Setup Local - Slotify

## Requisitos
- Docker + Docker Compose
- .NET 10 SDK (opcional si usas Docker)
- Node 22+ (opcional si usas Docker)
- Git

## Opción 1: Docker (Recomendado)

```bash
# Clonar repo
git clone https://github.com/jjac10/slotify.git
cd slotify

# Iniciar todo (backend + frontend + postgres)
docker-compose up --build

# En otra terminal, correr migraciones
docker exec slotify_api dotnet ef database update
```

**Acceso:**
- Frontend: http://localhost:3000
- Backend API: http://localhost:5000
- PostgreSQL: localhost:5432

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
```bash
cd backend
dotnet test
```

### Frontend
```bash
cd frontend
npm run test          # Unit tests
npm run test:e2e      # Playwright E2E
```

## CI/CD
GitHub Actions configurado para:
- Ejecutar tests en cada push
- Build Docker en main
- Deploy automático a Ionos (manual config)
