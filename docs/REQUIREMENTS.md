# Requisitos Funcionales y No-Funcionales - Slotify

## 1. Requisitos Funcionales

### 1.1 Autenticación y Usuarios

**RF-AUTH-001:** Registro de negocio
- Crear cuenta con email + contraseña
- Validar email único
- **Contraseña segura:** mín. 8 caracteres con mayúscula, minúscula, dígito y símbolo (si no → 400)
- Contraseña hasheada (bcrypt)
- Plan asignado automáticamente (Free)

**RF-AUTH-002:** Login
- Email + password
- Generar JWT token
- Refresh token (7 días)

**RF-AUTH-003:** Recuperación de contraseña
- Link de reset por email
- Válido 1 hora
- Nueva contraseña hasheada

### 1.2 Reservas (Guest & Registered)

**RF-RES-001:** Reserva como invitado
- Nombre + (Teléfono XOR Email)
- Sin crear cuenta
- Email/SMS confirmación
- ID único para historial futuro (teléfono o email)

**RF-RES-002:** Reserva registrado
- Usuario logueado elige servicio + fecha/hora
- Sistema sugiere slots disponibles
- Confirmación inmediata

**RF-RES-003:** Prevención doble booking
- Lock optimista en BD
- Check concurrencia en tiempo real
- Rechazar si slot ocupado
- Atomicidad garantizada

**RF-RES-004:** Gestión de reservas
- Ver historial (guest: por teléfono/email, user: en dashboard)
- Cancelar con X horas de anticipación
- Modificar si hay disponibilidad
- Notificación de cambios

### 1.3 Negocio (Business Owner)

**RF-BIZ-001:** Crear servicio
- Nombre, duración, precio (opcional)
- Múltiples servicios por negocio
- Límite: Free=5, Premium=ilimitado

**RF-BIZ-002:** Gestionar horario laboral
- Horario diario (lunes-domingo)
- Descansos dentro del día
- Días festivos/cerrado
- Zona horaria del negocio

**RF-BIZ-003:** Dashboard propietario
- Calendario de reservas
- Confirmación/cancelación manual
- Reportes (pending en Premium)
- Notificaciones de nuevas reservas

**RF-BIZ-004:** Personalización
- Logo + colores corporativos
- Mensaje de bienvenida
- Términos y condiciones
- Política de cancelación

### 1.4 Notificaciones

**RF-NOT-001:** Confirmación de reserva
- Email: detalles + link para modificar/cancelar
- SMS (Free: email, Premium: SMS+WhatsApp)
- Para guest y owner

**RF-NOT-002:** Recordatorio
- 24 horas antes
- 1 hora antes
- Configurable por owner

**RF-NOT-003:** Cancelación
- Notificar cambios a ambos

---

## 2. Requisitos No-Funcionales

### 2.1 Rendimiento
- **Tiempo respuesta API:** <200ms (p95)
- **Throughput:** 100 concurrentes sin degradación
- **TTFB:** <100ms para homepage

### 2.2 Seguridad
- **HTTPS:** Obligatorio en producción
- **Encriptación:** Datos sensibles en BD (teléfono, email invitados)
- **CORS:** Solo dominio propio
- **Rate limiting:** 100 req/min por IP
- **Validación:** Input sanitization todo
- **OWASP Top 10:** Mitigado

### 2.3 Disponibilidad
- **Uptime:** 99.5% (SLA futuro)
- **Backup:** Diario a S3 (futuro)
- **Disaster Recovery:** RTO 4h, RPO 1h (futuro)

### 2.4 Escalabilidad
- **BD:** Preparada para 1M+ reservas/mes
- **API:** Stateless (escala horizontal)
- **Storage:** Extensible a CDN (futuro)

### 2.5 Usabilidad
- **PWA:** Funciona offline (caché básico)
- **Responsive:** Móvil-first
- **Accesibilidad:** WCAG 2.1 AA (futuro)

---

## 3. Casos de Uso Principales

### UC-1: Reserva sin registro (Happy Path)
```
1. Guest accede a landing
2. Elige servicio
3. Sistema muestra slots disponibles
4. Guest selecciona slot
5. Ingresa nombre + teléfono/email
6. Sistema envía confirmación
7. Owner recibe notificación
```

### UC-2: Doble booking prevention
```
1. Guest A selecciona slot 14:00
2. Mismo tiempo, Guest B selecciona 14:00
3. Sistema bloquea uno (timestamp DB)
4. El segundo recibe "slot no disponible"
```

### UC-3: Modificar reserva
```
1. Guest accede por link en email
2. Ve detalles de su reserva
3. Elige nuevo slot disponible
4. Sistema valida y actualiza
5. Notifica owner + guest
```

---

## 4. Limitaciones Plan Freemium

| Feature | Free | Premium |
|---------|------|---------|
| Reservas/mes | 100 | Ilimitadas |
| Servicios | 5 | Ilimitados |
| Clientes | 50 | Ilimitados |
| Trabajadores | 1 | Ilimitados |
| Canales notificación | Email | Email+SMS+WhatsApp |
| Reportes | No | Sí |
| API access | No | Sí |
| Custom domain | No | Sí (futuro) |

---

## 5. Restricciones Técnicas

- BD: PostgreSQL 17 (Code First, EF Core migrations)
- JWT: HS256, exp 1 día, refresh 7 días
- Zonas horarias: UTC en BD, local en UI
- Divisas: Inicialmente sin pagos; arquitectura preparada para futuro
- Máx. 10 slots simultáneos por servicio (configurable)
