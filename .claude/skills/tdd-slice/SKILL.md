---
name: tdd-slice
description: Implementa una funcionalidad de Slotify como slice TDD completo (rojo → verde → refactor → verificar → commit atómico) siguiendo las convenciones del proyecto. Usar siempre que se pida añadir o modificar funcionalidad de backend o frontend.
---

# Slice TDD en Slotify

Todo cambio de funcionalidad se implementa como un **slice vertical** con TDD estricto.
Nunca se escribe código de producción sin un test en rojo que lo justifique.

## Flujo

1. **🔴 RED — tests primero**
   - Unit tests en `backend/Slotify.Tests/Unit/` (xUnit + Moq contra interfaces del dominio).
   - Tests de integración en `backend/Slotify.Tests/Integration/` (WebApplicationFactory +
     Testcontainers PostgreSQL — necesitan Docker en marcha).
   - Ejecuta `dotnet test` y confirma que los tests nuevos **fallan por la razón esperada**.

2. **🟢 GREEN — mínimo código**
   - Lógica de negocio en `Slotify.Domain/Services/` detrás de interfaces (`Slotify.Domain/Interfaces/`).
   - Acceso a datos vía Repository Pattern en `Slotify.Infrastructure/` (nunca `DbContext` en dominio).
   - Controllers delgados en `Slotify.API/Controllers/`: validan, delegan al servicio, mapean a DTOs.
   - Si cambia el modelo: migración EF (`dotnet ef migrations add Nombre_Descriptivo`).

3. **♻️ REFACTOR** — con los tests en verde, limpia duplicación y nombres. Vuelve a ejecutar los tests.

4. **Frontend (si el slice tiene UI)**
   - Cliente tipado en `frontend/src/services/`, tipos en `frontend/src/types/`.
   - Páginas/componentes con Tailwind usando los tokens del design system
     (ver `docs/stitch_slotify_local_booking_platform/modern_booking_system/DESIGN.md`).
   - Spec e2e Playwright en `frontend/tests/e2e/` cubriendo el camino feliz.

5. **Verificar todo**: `dotnet test` + `npm run build` (typecheck incluido) + `npx playwright test <spec>`.

6. **Commit atómico** con Conventional Commits en español descriptivo:
   `feat(auth): añadir recuperación de contraseña` · `test(reservations): reservas concurrentes`.

## Reglas del proyecto

- UTC en BD, hora local solo en UI.
- Datos personales de invitados: cifrados AES-256-GCM + blind index HMAC; **nunca en la URL**.
- No romper los usuarios demo (`owner@demo.slotify` / `cliente@demo.slotify`) ni los specs e2e existentes.
- Español en textos de UI y documentación; inglés en identificadores de código.
