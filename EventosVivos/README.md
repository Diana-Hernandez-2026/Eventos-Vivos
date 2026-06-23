# EventosVivos — Sistema de Reservas de Eventos

Prueba Técnica Fullstack · .NET 9 + Angular 19

---

## Arquitectura Elegida

### Backend — Clean Architecture + CQRS

```
EventosVivos.Domain          → Entidades, enums, interfaces, excepciones de dominio
EventosVivos.Application     → Comandos/Queries (MediatR), validadores (FluentValidation), DTOs
EventosVivos.Infrastructure  → EF Core + SQLite, repositorios, seeds
EventosVivos.API             → Controllers, middleware, JWT/OIDC, configuración
EventosVivos.Tests           → Pruebas unitarias (xUnit + Moq + FluentAssertions)
```

**Justificación:** CQRS con MediatR separa lecturas de escrituras de forma explícita, facilitando el escalado horizontal (los query handlers pueden ir a réplicas de lectura, los command handlers al nodo primario). Clean Architecture garantiza que las reglas de negocio viven en Domain/Application, independientes del transporte HTTP o la base de datos concreta.

### Frontend — Angular 19 (Standalone Components)

SPA con lazy-loading por ruta, interceptor JWT centralizado, y Angular Signals para el estado de autenticación.

---

## Tecnologías

| Capa | Tecnología |
|---|---|
| Backend | .NET 9, ASP.NET Core |
| ORM | Entity Framework Core 9 + SQLite |
| CQRS | MediatR 12 |
| Validación | FluentValidation 11 |
| Autenticación | JWT Bearer + Microsoft OIDC (Azure AD) |
| Logging | NLog 5 |
| Versionado API | Asp.Versioning.Mvc 8 |
| Documentación | Swagger / Swashbuckle |
| Tests | xUnit, Moq, FluentAssertions, EF InMemory |
| Frontend | Angular 19, TypeScript |

---

## Cómo ejecutar localmente

### Prerequisitos

- .NET 9 SDK
- Node.js 22+ / npm 10+

### 1. Configurar credenciales OAuth2 (Microsoft / Azure AD)

1. Ve a [Azure Portal](https://portal.azure.com/) → Azure Active Directory → App registrations → New registration
2. Agrega `http://localhost:4200/auth/callback` como Redirect URI (tipo "Web")
3. En "Certificates & secrets" crea un client secret
4. Copia Application (client) ID y el secret

#### Configurar en el proyecto

**Backend** — edita `src/EventosVivos.API/appsettings.Development.json`:
```json
{
  "OAuth": {
    "Microsoft": {
      "ClientId": "TU_MICROSOFT_CLIENT_ID",
      "ClientSecret": "TU_MICROSOFT_CLIENT_SECRET"
    }
  }
}
```

**Frontend** — edita `frontend/src/environments/environment.ts`:
```ts
export const environment = {
  production: false,
  apiUrl: 'http://localhost:5000/api/v1',
  microsoftClientId: 'TU_MICROSOFT_CLIENT_ID'
};
```

### 2. Ejecutar el Backend

```bash
cd EventosVivos

# Ejecutar la API (crea la DB SQLite y siembra los venues automáticamente)
dotnet run --project src/EventosVivos.API

# La API queda disponible en:
#   http://localhost:5000
#   Swagger UI: http://localhost:5000/swagger
```

### Tests

```bash
dotnet test
# Resultado esperado: 23 tests, 0 failures
```

### 3. Ejecutar el Frontend

```bash
cd frontend
npm install
npm start
# Disponible en http://localhost:4200
```

---

## Endpoints REST (v1)

### Autenticación (Authorization Code Flow — Microsoft)
| Método | Ruta | Descripción |
|---|---|---|
| POST | `/api/v1/auth/microsoft/exchange` | Recibe `{code, redirectUri}` → intercambia con Microsoft → emite JWT |

**Flujo completo:**
1. Usuario hace clic en "Continuar con Microsoft"
2. Angular redirige a Azure AD con `client_id`, `redirect_uri`, `scope`, `state` (CSRF token)
3. Microsoft autentica y redirige a `/auth/callback?code=...&state=...`
4. El componente callback envía el código al backend
5. El backend intercambia el código por tokens usando el `client_secret` (nunca expuesto al cliente)
6. Backend parsea el `id_token`, extrae email/nombre, emite JWT propio
7. Frontend almacena el JWT y redirige a `/events`

### Venues (público)
| Método | Ruta | Descripción |
|---|---|---|
| GET | `/api/v1/venues` | Lista venues de referencia |

### Eventos
| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| GET | `/api/v1/events` | ✗ | Lista con filtros + cursor pagination |
| POST | `/api/v1/events` | ✓ | Crear evento (RF-01) |
| GET | `/api/v1/events/{id}/report` | ✗ | Reporte de ocupación (RF-06) |

**Filtros disponibles en GET /events:**
`type`, `startFrom`, `startTo`, `venueId`, `status`, `titleSearch`, `cursor`, `limit`

### Reservas
| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| POST | `/api/v1/reservations` | ✓ | Crear reserva (RF-03) |
| POST | `/api/v1/reservations/{id}/confirm` | ✓ | Confirmar pago (RF-04) |
| POST | `/api/v1/reservations/{id}/cancel` | ✓ | Cancelar reserva (RF-05) |

**Idempotencia:** Incluye el header `Idempotency-Key: <UUID>` en POST/PUT/PATCH para obtener respuestas idempotentes.

---

## Reglas de negocio implementadas

| ID | Regla | Implementada en |
|---|---|---|
| RN-01 | Capacidad máxima ≤ capacidad del venue | `CreateEventCommandHandler` |
| RN-02 | Sin superposición de horarios por venue | `EventRepository.HasVenueOverlapAsync` |
| RN-03 | Weekends no pueden iniciar después de 22:00 | `CreateEventCommandHandler` |
| RN-04 | No se reserva si el evento inicia en < 1h | `CreateReservationCommandHandler` |
| RN-05 | Precio > $100 → máximo 10 entradas/transacción | `CreateReservationCommandHandler` |
| RN-06 | Evento se marca `completado` si EndDateTime < now | `EventRepository.UpdateCompletedStatusAsync` |
| RN-07 | Cancelación < 48h → entradas marcadas como "perdidas" | `CancelReservationCommandHandler` |

---

## Características técnicas destacadas

### Cursor-based Pagination
El cursor codifica en base64 `createdAt|id` del último ítem. Esto permite paginación estable sin OFFSET, apto para producción con grandes volúmenes.

### Idempotencia
Middleware que intercepta POST/PUT/PATCH con header `Idempotency-Key`. Almacena la respuesta en `IdempotencyRecords` (SQLite) con TTL de 24h. Peticiones duplicadas reciben exactamente la misma respuesta sin reejecutar la lógica de negocio.

### Manejo de errores
`ExceptionHandlingMiddleware` mapea excepciones de dominio a códigos HTTP apropiados (`422`, `404`, `409`) y retorna `application/problem+json`. En desarrollo incluye stacktrace; en producción solo el mensaje.

### JWT + OIDC (Authorization Code Flow — Microsoft)
El frontend redirige a Azure AD con un parámetro `state` aleatorio guardado en `sessionStorage` como protección CSRF. Al regresar, el backend recibe el `code` y lo intercambia server-side (usando el `client_secret` que nunca sale del servidor) por tokens de Microsoft. El `id_token` retornado es un JWT firmado por Microsoft — se parsea para extraer email y nombre, y se emite un JWT propio de la aplicación. Todos los endpoints de escritura requieren `[Authorize]`.

### Azure App Services
La API está lista para despliegue en Azure App Services:
- `ASPNETCORE_ENVIRONMENT=Production` oculta detalles de errores
- Connection string configurable vía App Settings
- SQLite funciona en el filesystem de App Service (o migrar a Azure SQL para producción real)

---

## Estructura de archivos

```
EventosVivos/
├── src/
│   ├── EventosVivos.Domain/
│   │   ├── Entities/        (Venue, Event, Reservation, IdempotencyRecord)
│   │   ├── Enums/           (EventType, EventStatus, ReservationStatus)
│   │   ├── Interfaces/      (IEventRepository, IReservationRepository, ...)
│   │   └── Exceptions/      (DomainException, NotFoundException, ConflictException)
│   ├── EventosVivos.Application/
│   │   ├── Events/          (CreateEvent, GetEvents, GetOccupancyReport)
│   │   ├── Reservations/    (CreateReservation, ConfirmPayment, CancelReservation)
│   │   ├── Venues/          (GetVenues)
│   │   ├── Behaviors/       (ValidationBehavior, LoggingBehavior)
│   │   └── Common/          (CursorPage, CursorEncoder)
│   ├── EventosVivos.Infrastructure/
│   │   └── Persistence/     (AppDbContext, configurations, repositories, seeding)
│   └── EventosVivos.API/
│       ├── Controllers/V1/  (EventsController, ReservationsController, AuthController, VenuesController)
│       ├── Middleware/      (ExceptionHandlingMiddleware, IdempotencyMiddleware)
│       └── Configuration/   (JwtSettings)
├── tests/
│   └── EventosVivos.Tests/
│       ├── Events/          (CreateEventTests, OccupancyReportTests)
│       └── Reservations/    (CreateReservationTests, ConfirmAndCancelReservationTests)
└── frontend/               (Angular 19 SPA)
    └── src/app/
        ├── core/            (models, services, interceptors)
        └── pages/           (login, events, reservations)
```
