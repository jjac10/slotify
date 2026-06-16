# Slotify Backend

ASP.NET Core 10 API para Slotify.

## Stack
- **Framework:** ASP.NET Core 10
- **ORM:** Entity Framework Core 10
- **Database:** PostgreSQL 17
- **Testing:** xUnit + Moq

## Estructura
```
backend/
├── Slotify.API/           # Controllers + Program.cs
├── Slotify.Domain/        # Entities + Interfaces
├── Slotify.Infrastructure/ # Repositories + DbContext
├── Slotify.Tests/         # Unit & Integration Tests
```

## Ejecutar Localmente
```bash
cd backend
dotnet restore
dotnet run
```

API estará en `http://localhost:5000`

## Base de Datos
Migraciones con EF Core:
```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

## Testing
```bash
dotnet test
```
