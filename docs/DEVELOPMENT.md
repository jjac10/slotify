# Guía de Desarrollo - Slotify

## Estructura del Proyecto

```
slotify/
├── backend/
│   ├── Slotify.API/              # Controllers, Program.cs, middleware
│   ├── Slotify.Domain/           # Entities, Interfaces, DTOs
│   ├── Slotify.Infrastructure/   # DbContext, Repositories, Services
│   ├── Slotify.Tests/            # xUnit tests
│   └── Slotify.sln               # Solution file
├── frontend/
│   ├── src/
│   │   ├── components/           # React components
│   │   ├── pages/                # Page components
│   │   ├── hooks/                # Custom hooks
│   │   ├── services/             # API calls
│   │   ├── types/                # TypeScript interfaces
│   │   └── App.tsx
│   ├── tests/                    # Vitest + RTL tests
│   ├── vite.config.ts
│   └── package.json
├── infra/
│   ├── Dockerfile.backend
│   ├── Dockerfile.frontend
│   └── nginx.conf
├── docs/
│   ├── REQUIREMENTS.md           # Casos de uso, requisitos
│   ├── DATA_MODEL.md             # Schema BD, tablas
│   ├── API.md                    # Endpoints documentados
│   ├── ARCHITECTURE.md           # Decisiones arquitectura
│   ├── DATABASE.md               # Estrategia BD, migrations
│   ├── SETUP.md                  # Dev setup local
│   ├── DEVELOPMENT.md            # Esta guía
│   └── TESTING.md                # Estrategia testing (futuro)
├── docker-compose.yml
├── CLAUDE.md                     # Instrucciones para IA
├── README.md                     # Overview
└── .gitignore
```

---

## Workflow de Desarrollo (TDD)

### 1. Leer Requisitos
- Consultar `docs/REQUIREMENTS.md` para entender el caso de uso
- Verificar endpoints en `docs/API.md`
- Revisar modelo de datos en `docs/DATA_MODEL.md`

### 2. Escribir Test Primero (Red)
**Backend:**
```csharp
[Fact]
public async Task CreateReservation_WithGuestPhone_ShouldEncryptPhone()
{
    // Arrange
    var request = new CreateReservationRequest
    {
        GuestName = "Juan",
        GuestPhone = "+34912345678"
    };
    
    // Act
    var result = await _reservationService.CreateAsync(request);
    
    // Assert
    Assert.NotNull(result.Id);
    // Verificar que el teléfono está encriptado en BD
}
```

**Frontend:**
```typescript
describe('ReservationForm', () => {
  it('should show phone input by default', () => {
    const { getByLabelText } = render(<ReservationForm />);
    expect(getByLabelText(/Teléfono/i)).toBeInTheDocument();
  });
  
  it('should toggle to email when user clicks toggle', () => {
    const { getByLabelText, getByRole } = render(<ReservationForm />);
    fireEvent.click(getByRole('button', { name: /Usar email/i }));
    expect(getByLabelText(/Email/i)).toBeInTheDocument();
  });
});
```

### 3. Código Mínimo (Green)
Implementar solo lo necesario para pasar el test.

### 4. Refactor
Mejorar sin cambiar comportamiento. Ejecutar tests continuamente.

### 5. Commit Atómico
```bash
git add .
git commit -m "feat(reservations): encrypt guest phone on creation"
```

---

## Estructura Backend

### Layers

```
API (Controllers)
    ↓ (HTTP → DTO)
Domain (Entities, Services)
    ↓ (Lógica de negocio)
Infrastructure (Repositories, DbContext)
    ↓ (SQL)
PostgreSQL
```

### Ejemplo: Crear Reserva

**1. Endpoint (API/Controllers/ReservationsController.cs)**
```csharp
[HttpPost]
[AllowAnonymous]
public async Task<ActionResult<ReservationDto>> Create(CreateReservationRequest request)
{
    var result = await _reservationService.CreateAsync(request);
    return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
}
```

**2. Service (Domain/Services/ReservationService.cs)**
```csharp
public async Task<ReservationDto> CreateAsync(CreateReservationRequest request)
{
    // Validar disponibilidad
    var isAvailable = await _reservationRepository.IsSlotAvailableAsync(...);
    if (!isAvailable) throw new SlotUnavailableException();
    
    // Encriptar datos sensibles
    var encryptedPhone = _cryptoService.Encrypt(request.GuestPhone);
    
    // Crear entidad
    var reservation = new Reservation { ... };
    
    // Persistir
    await _reservationRepository.AddAsync(reservation);
    
    // Enviar notificación (async, no bloquea respuesta)
    _ = _notificationService.SendConfirmationAsync(reservation);
    
    return _mapper.Map<ReservationDto>(reservation);
}
```

**3. Repository (Infrastructure/Repositories/ReservationRepository.cs)**
```csharp
public async Task<bool> IsSlotAvailableAsync(Guid serviceId, DateTime start, DateTime end)
{
    return !await _context.Reservations
        .Where(r => r.ServiceId == serviceId && r.Status != ReservationStatus.Cancelled)
        .AnyAsync(r => r.StartTime < end && r.EndTime > start);
}
```

**4. Entity (Domain/Entities/Reservation.cs)**
```csharp
public class Reservation
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public Guid ServiceId { get; set; }
    public string GuestName { get; set; }
    public string? GuestPhoneEncrypted { get; set; }
    public string? GuestEmailEncrypted { get; set; }
    public DateTime StartTime { get; set; } // UTC
    public DateTime EndTime { get; set; } // UTC
    public ReservationStatus Status { get; set; }
    public int Version { get; set; } // Optimistic locking
}
```

---

## Estructura Frontend

### Componentes Core

```
src/
├── components/
│   ├── ReservationForm.tsx       # Formulario reserva
│   ├── AvailableSlots.tsx        # Grid de horarios
│   ├── ConfirmationModal.tsx     # Confirmación
│   └── ...
├── pages/
│   ├── BookingPage.tsx           # Flujo reserva
│   ├── BusinessDashboard.tsx     # Dashboard propietario
│   └── ...
├── services/
│   ├── api.ts                    # Axios client
│   ├── reservationService.ts     # API calls
│   └── ...
└── types/
    ├── models.ts                 # TypeScript interfaces
    └── ...
```

### Ejemplo: Componente Reserva

```typescript
export const ReservationForm: React.FC<{ businessId: string }> = ({ businessId }) => {
  const [method, setMethod] = useState<'phone' | 'email'>('phone');
  const [loading, setLoading] = useState(false);
  
  const onSubmit = async (data: ReservationFormData) => {
    try {
      setLoading(true);
      const payload = {
        ...data,
        guestPhone: method === 'phone' ? data.phone : undefined,
        guestEmail: method === 'email' ? data.email : undefined,
      };
      
      const result = await reservationService.create(payload);
      navigate(`/confirm/${result.confirmationToken}`);
    } catch (error) {
      setError(error.message);
    } finally {
      setLoading(false);
    }
  };
  
  return (
    <form onSubmit={handleSubmit(onSubmit)}>
      {/* Toggle teléfono/email */}
      <button type="button" onClick={() => setMethod(method === 'phone' ? 'email' : 'phone')}>
        Cambiar a {method === 'phone' ? 'email' : 'teléfono'}
      </button>
      
      {method === 'phone' ? (
        <input {...register('phone', { required: true })} />
      ) : (
        <input {...register('email', { required: true })} type="email" />
      )}
      
      <button type="submit" disabled={loading}>
        {loading ? 'Reservando...' : 'Reservar'}
      </button>
    </form>
  );
};
```

---

## Testing Strategy

### Backend (xUnit)

**Tipos de tests:**
1. **Unit:** Servicios sin BD (Mock repositories)
2. **Integration:** Con BD real (TestContainer PostgreSQL)
3. **API:** HTTP endpoint testing

**Estructura:**
```
Slotify.Tests/
├── Unit/
│   ├── Services/
│   │   └── ReservationServiceTests.cs
│   └── ...
├── Integration/
│   ├── Repositories/
│   │   └── ReservationRepositoryTests.cs
│   └── ...
└── TestFixtures/
    ├── DatabaseFixture.cs
    └── FactoryBoy.cs (data builders)
```

### Frontend (Vitest + RTL)

**Tipos de tests:**
1. **Unit:** Componentes aislados
2. **Integration:** Flujos multicomponente
3. **E2E:** Playwright (flujo completo)

**Estructura:**
```
frontend/tests/
├── unit/
│   └── ReservationForm.test.tsx
├── integration/
│   └── BookingFlow.test.tsx
└── e2e/
    └── booking.spec.ts (Playwright)
```

---

## Git Workflow

### Branches
```
main                          (producción)
├── develop                   (staging)
│   ├── feat/guest-booking
│   ├── feat/notifications
│   └── fix/double-booking
```

### Commit Messages (Conventional Commits)
```
feat(reservations): add guest booking without registration
├─ BREAKING CHANGE: changed reservation response format
├─ Fixes #42
├─ Co-authored-by: Jane Doe <jane@example.com>

test(reservations): add concurrent booking prevention test
fix(notifications): handle email delivery timeout
docs(api): add booking endpoint documentation
infra(docker): update postgres to version 17
refactor(auth): extract token generation to service
```

### PR/MR Checklist
- [ ] Tests pasar localmente
- [ ] Code review + approval
- [ ] Migrations reversibles (si aplica)
- [ ] Documentación actualizada
- [ ] No secrets en commit

---

## Running Locally

### Setup Inicial
```bash
git clone https://github.com/jjac10/slotify.git
cd slotify

# Backend
cd backend
dotnet restore
dotnet ef database update

# Frontend
cd ../frontend
npm install
```

### Development Servers
```bash
# Terminal 1: Backend
cd backend && dotnet run

# Terminal 2: Frontend
cd frontend && npm run dev

# Terminal 3: PostgreSQL (si no usas Docker)
docker run -d ... postgres:17
```

### Docker Compose
```bash
docker-compose up --build
```

---

## Debugging

### Backend
```csharp
// En Program.cs
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Debug);
});
```

### Frontend
```typescript
// React DevTools
// Vite DevTools
// Network tab en Chrome
console.log('Debug:', variable);
```

---

## Performance Considerations

### Database
- Índices en `business_id, start_time` para availability queries
- Lazy loading de relaciones
- Pagination obligatoria para listas >20 items

### API
- Rate limiting: 100 req/min
- Caché de GET sin parámetros (1 hora)
- Compresión gzip

### Frontend
- Code splitting por rutas
- Lazy load imágenes
- Bundle size <300KB (gzipped)

---

## Common Issues & Solutions

### "Slot no disponible" aunque esté libre
→ Verificar timezone conversion (UTC ↔ local)

### Tests flakean en CI
→ Mock timestamps, no usar DateTime.Now
→ Usar TestContainers para BD aislada

### Double booking ocurre
→ Verificar unique constraint en BD
→ Check optimistic locking (version field)

---

## Recursos Útiles

- [Entity Framework Core Docs](https://learn.microsoft.com/en-us/ef/core/)
- [React Testing Library Best Practices](https://testing-library.com/docs/queries/about)
- [PostgreSQL Date/Time Functions](https://www.postgresql.org/docs/17/functions-datetime.html)
- [JWT Best Practices](https://tools.ietf.org/html/rfc8949)
