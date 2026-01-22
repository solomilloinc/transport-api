# DOMAIN.md - Modelo de Negocio

Este documento describe el modelo de dominio y las reglas de negocio del sistema de transporte.

## Contexto del Negocio

Sistema de gestión de transporte de pasajeros que permite:
- Definir rutas (trips) entre ciudades con precios configurables
- Crear servicios recurrentes con horarios
- Gestionar reservas de pasajeros
- Procesar pagos integrados con MercadoPago
- Generar reportes de viajes y cobros

---

## Entidades Principales

### Trip (Ruta con Precios)

Define una ruta entre dos ciudades con sus precios asociados.

```
Trip
├── TripId (PK)
├── Description
├── OriginCityId (FK → City)
├── DestinationCityId (FK → City)
├── Status (Active/Inactive)
└── Prices[] → TripPrice
```

**Reglas de Negocio:**
- Un Trip representa una ruta direccional (A → B es diferente de B → A)
- Los precios están en TripPrice, no en Trip directamente
- Para rutas IdaVuelta se necesitan dos Trips (A→B y B→A)

### TripPrice (Precio de Ruta)

Define el precio para un segmento específico del viaje.

```
TripPrice
├── TripPriceId (PK)
├── TripId (FK → Trip)
├── CityId (FK → City)         // Ciudad destino del precio
├── DirectionId (FK → Direction) // Opcional: parada específica
├── ReserveTypeId              // Ida, Vuelta, IdaVuelta
├── Price
├── Order                      // Para ordenar paradas intermedias
└── Status
```

**Reglas de Negocio:**
- `CityId` indica la ciudad destino del tramo:
  - Para **Ida**: CityId = DestinationCityId del Trip
  - Para **Vuelta**: CityId = OriginCityId del Trip
- `DirectionId` permite precios diferenciados por parada
- `Order` ordena las paradas para rutas con múltiples destinos

**Ejemplo de configuración de precios:**
```
Trip: Córdoba (1) → Buenos Aires (2)

TripPrice para IDA:
  - CityId: 2 (Buenos Aires = destino)
  - ReserveTypeId: Ida
  - Price: 5000

TripPrice para IDA Y VUELTA:
  - CityId: 2 (Buenos Aires = destino)
  - ReserveTypeId: IdaVuelta
  - Price: 9000

Trip: Buenos Aires (2) → Córdoba (1)

TripPrice para VUELTA (retorno):
  - CityId: 1 (Córdoba = destino del retorno)
  - ReserveTypeId: Ida
  - Price: 5000
```

### Reserve (Reserva)

Representa una reserva de viaje para una fecha y horario específico.

```
Reserve
├── ReserveId (PK)
├── ReserveDate
├── TripId (FK → Trip)         // Para obtener precios
├── ServiceId (FK → Service)   // Opcional: si viene de batch
├── ServiceScheduleId (FK)     // Opcional: horario del servicio
├── OriginId (FK → City)
├── DestinationId (FK → City)
├── VehicleId (FK → Vehicle)
├── DriverId (FK → Driver)     // Opcional
├── DepartureHour
├── EstimatedDuration
├── Status                     // Pending, Confirmed, InProgress, Completed, Cancelled
├── RowVersion                 // Concurrencia optimista
├── OriginName, DestinationName, ServiceName  // Desnormalizados
└── Passengers[] → Passenger
```

**Reglas de Negocio:**
- `TripId` es obligatorio para calcular precios de pasajeros
- `ServiceId` es opcional (reservas manuales no tienen servicio)
- `RowVersion` previene conflictos de concurrencia al actualizar
- Capacidad máxima viene del `Vehicle` asociado

**Estados de Reserve:**
```
Pending → Confirmed → InProgress → Completed
    ↓         ↓           ↓
    └─────────┴───────────┴──→ Cancelled
```

### Passenger (Pasajero)

Representa un pasajero individual en una reserva.

```
Passenger
├── PassengerId (PK)
├── ReserveId (FK → Reserve)
├── ReserveRelatedId (FK → Reserve)  // Para IdaVuelta
├── CustomerId (FK → Customer)       // Opcional
├── FirstName, LastName, DocumentNumber
├── Email, Phone
├── PickupLocationId (FK → Direction)
├── DropoffLocationId (FK → Direction)
├── PickupAddress, DropoffAddress
├── Price                            // Precio calculado
├── HasTraveled
└── Status                           // Confirmed, Cancelled
```

**Reglas de Negocio:**
- `ReserveRelatedId` conecta pasajeros de ida con vuelta
- `Price` se calcula al crear desde TripPrice
- `CustomerId` se asigna si el pasajero es cliente registrado

### Service (Servicio Recurrente)

Define una ruta con horarios recurrentes para generación automática de reservas.

```
Service
├── ServiceId (PK)
├── Name
├── OriginId (FK → City)
├── DestinationId (FK → City)
├── VehicleId (FK → Vehicle)
├── StartDay (DayOfWeek)      // Día inicio de semana
├── EndDay (DayOfWeek)        // Día fin de semana
├── EstimatedDuration
├── Status
├── Schedules[] → ServiceSchedule
├── Customers[] → ServiceCustomer
└── Reserves[] → Reserve
```

**Reglas de Negocio:**
- `StartDay/EndDay` definen qué días de la semana opera el servicio
- El batch `GenerateFutureReserves` crea reserves automáticamente
- Los precios NO están en Service, están en Trip/TripPrice

### ServiceSchedule (Horario de Servicio)

```
ServiceSchedule
├── ServiceScheduleId (PK)
├── ServiceId (FK → Service)
├── DepartureHour (TimeSpan)
└── Status
```

---

## Flujos de Negocio

### 1. Creación de Reserva Manual (CreateReserve)

```
1. Recibir ReserveCreateDto (TripId, OriginId, DestinationId, VehicleId, DepartureHour, ReserveDate)
2. Validar datos con FluentValidation
3. Buscar Trip para obtener nombres de ciudades
4. Crear Reserve con datos desnormalizados
5. SaveChangesAsync
6. Retornar ReserveId
```

### 2. Agregar Pasajeros a Reserva (CreatePassengerReserves)

```
1. Recibir lista de PassengerReserveCreateRequestDto
2. Validar que Reserve existe y tiene capacidad
3. Para cada pasajero:
   a. Buscar/Crear Customer si tiene documentNumber
   b. Obtener precio de TripPrice:
      - Buscar Trip por OriginCityId/DestinationCityId
      - Filtrar TripPrice por CityId (destino) y ReserveTypeId
   c. Crear Passenger con precio calculado
4. Si es IdaVuelta:
   a. Crear pasajero en reserva de ida
   b. Crear pasajero en reserva de vuelta con ReserveRelatedId
5. SaveChangesWithOutboxAsync (para eventos)
6. Enviar email de confirmación
```

### 3. Generación Batch de Reservas (GenerateFutureReserves)

```
1. Timer trigger (diario)
2. Para cada Service activo:
   a. Verificar si el día actual está en rango (StartDay-EndDay)
   b. Buscar Trip por OriginId/DestinationId del Service
   c. Para cada Schedule:
      - Crear Reserve para los próximos N días
      - Asignar TripId, ServiceId, ServiceScheduleId
3. SaveChangesAsync
```

### 4. Flujo de Pago Externo (Lock → Create)

```
1. LockReserveSlots:
   a. Validar capacidad disponible
   b. Crear ReserveSlotLock con token único
   c. Retornar token y expiración

2. (Cliente completa pago en MercadoPago)

3. CreatePassengerReservesWithLock:
   a. Validar token no expirado
   b. Validar capacidad aún disponible
   c. Crear pasajeros
   d. Liberar lock
   e. Crear ReservePayment

4. (Si timeout) CancelReserveSlotLock:
   a. Eliminar lock
   b. Liberar capacidad
```

---

## Reglas de Validación

### Reserve
- `ReserveDate` no puede ser en el pasado
- `TripId` debe existir y estar activo
- `VehicleId` debe existir y estar activo
- Capacidad no puede exceder `Vehicle.Capacity`

### Passenger
- `DocumentNumber` es obligatorio
- `FirstName` y `LastName` son obligatorios
- Para `IdaVuelta`, ambas reservas (ida y vuelta) deben existir
- El precio debe coincidir con TripPrice configurado

### TripPrice
- `CityId` debe ser válido para el Trip:
  - Para Ida: CityId = Trip.DestinationCityId
  - Para Vuelta (en trip inverso): CityId = Trip.DestinationCityId
- `ReserveTypeId` debe ser Ida, Vuelta, o IdaVuelta
- `Price` debe ser > 0

---

## Enumeraciones

### ReserveStatusEnum
```csharp
Pending = 1,      // Reserva creada, sin confirmar
Confirmed = 2,    // Reserva confirmada
InProgress = 3,   // Viaje en curso
Completed = 4,    // Viaje completado
Cancelled = 5     // Reserva cancelada
```

### ReserveTypeIdEnum
```csharp
Ida = 1,          // Solo ida
Vuelta = 2,       // Solo vuelta
IdaVuelta = 3     // Ida y vuelta (requiere dos reserves)
```

### PassengerStatusEnum
```csharp
Confirmed = 1,    // Pasajero confirmado
Cancelled = 2     // Pasajero cancelado
```

### EntityStatusEnum
```csharp
Active = 1,       // Entidad activa
Inactive = 2      // Entidad inactiva (soft delete)
```

---

## Consideraciones de Concurrencia

### Optimistic Locking en Reserve
- `RowVersion` (timestamp) previene escrituras conflictivas
- Si dos usuarios modifican la misma reserve, el segundo recibe error
- El cliente debe recargar y reintentar

### Slot Locking
- `ReserveSlotLock` previene overbooking durante checkout
- Locks expiran automáticamente (`ReserveOption.SlotLockTimeoutMinutes`)
- Token único permite identificar el lock

---

## Eventos de Dominio

| Evento | Trigger | Acción |
|--------|---------|--------|
| `ReserveCreatedEvent` | Reserve creada | Notificar sistema |
| `PassengerCreatedEvent` | Pasajero agregado | Enviar email confirmación |
| `PaymentConfirmedEvent` | Pago confirmado | Actualizar estado reserve |

Los eventos se publican vía **Outbox Pattern** para garantizar consistencia.
