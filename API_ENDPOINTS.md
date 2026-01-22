# API_ENDPOINTS.md - Referencia de Endpoints

Referencia rápida de todos los endpoints de la API de transporte.

> **Nota:** Todos los endpoints (excepto los marcados como `[Public]`) requieren token JWT en header `Authorization: Bearer {token}`.

---

## Trip (Rutas y Precios)

### CRUD de Trips

| Método | Ruta | Descripción | Auth |
|--------|------|-------------|------|
| POST | `/api/trip-create` | Crear nuevo trip | Admin |
| PUT | `/api/trip-update/{tripId}` | Actualizar trip | Admin |
| DELETE | `/api/trip-delete/{tripId}` | Eliminar trip | Admin |
| GET | `/api/trip/{tripId}` | Obtener trip por ID | Admin |
| POST | `/api/trip-report` | Reporte paginado de trips | Admin |

**TripCreateDto:**
```json
{
  "description": "Córdoba - Buenos Aires",
  "originCityId": 1,
  "destinationCityId": 2
}
```

### Gestión de Precios

| Método | Ruta | Descripción | Auth |
|--------|------|-------------|------|
| POST | `/api/trip-price-add` | Agregar precio a trip | Admin |
| PUT | `/api/trip-price-update/{tripPriceId}` | Actualizar precio | Admin |
| DELETE | `/api/trip-price-delete/{tripPriceId}` | Eliminar precio | Admin |
| PUT | `/api/trip-prices-update-percentage` | Actualización masiva % | Admin |

**TripPriceCreateDto:**
```json
{
  "tripId": 1,
  "cityId": 2,              // Ciudad destino del tramo
  "directionId": null,      // Opcional: parada específica
  "reserveTypeId": 1,       // 1=Ida, 2=Vuelta, 3=IdaVuelta
  "price": 5000.00,
  "order": 0
}
```

**TripPriceUpdateDto:**
```json
{
  "price": 5500.00
}
```

---

## Reserve (Reservas)

### CRUD de Reservas

| Método | Ruta | Descripción | Auth |
|--------|------|-------------|------|
| POST | `/api/reserve-create` | Crear reserva manual | Admin |
| PUT | `/api/reserve-update/{reserveId}` | Actualizar reserva | Admin |
| POST | `/api/reserve-report/{reserveDate}` | Reporte por fecha (yyyyMMdd) | Admin |

**ReserveCreateDto:**
```json
{
  "tripId": 1,
  "originId": 1,
  "destinationId": 2,
  "vehicleId": 1,
  "driverId": null,
  "departureHour": "08:00:00",
  "estimatedDuration": "04:00:00",
  "reserveDate": "2025-02-01"
}
```

### Gestión de Pasajeros

| Método | Ruta | Descripción | Auth |
|--------|------|-------------|------|
| POST | `/api/passenger-reserves-create` | Agregar pasajeros a reserva | Admin |
| PUT | `/api/passenger-reserve-update/{passengerId}` | Actualizar pasajero | Admin |
| POST | `/api/passenger-reserve-report/{reserveId}` | Reporte de pasajeros | Admin |

**PassengerReserveCreateRequestWrapperDto:**
```json
{
  "reserveId": 1,
  "reserveIdVuelta": 2,     // Opcional: para IdaVuelta
  "passengers": [
    {
      "firstName": "Juan",
      "lastName": "Pérez",
      "documentNumber": "12345678",
      "email": "juan@email.com",
      "phone": "1234567890",
      "reserveTypeId": 1,
      "pickupLocationId": null,
      "dropoffLocationId": null
    }
  ]
}
```

### Pagos

| Método | Ruta | Descripción | Auth |
|--------|------|-------------|------|
| POST | `/api/reserve-payments-create/{reserveId}/{customerId}` | Crear pagos | Admin |
| POST | `/api/reserve-payment-summary/{reserveId}` | Resumen de pagos | Admin |

---

## Flujo Público (Checkout Externo)

Endpoints para integración con checkout externo (MercadoPago).

| Método | Ruta | Descripción | Auth |
|--------|------|-------------|------|
| POST | `/api/reserve-slots-lock` | Bloquear asientos | [Public] |
| POST | `/api/passenger-reserves-create-with-lock` | Crear con lock | [Public] |
| DELETE | `/api/reserve-slots-lock/{lockToken}` | Cancelar lock | [Public] |
| POST | `/api/public/reserve-summary/` | Resumen público | [Public] |

**LockReserveSlotsRequestDto:**
```json
{
  "reserveId": 1,
  "reserveIdVuelta": 2,     // Opcional
  "passengers": 3,
  "passengersVuelta": 3     // Opcional
}
```

**CreateReserveWithLockRequestDto:**
```json
{
  "lockToken": "guid-token",
  "reserveId": 1,
  "reserveIdVuelta": 2,
  "passengers": [...],
  "paymentId": "mp-payment-id"
}
```

---

## Service (Servicios Recurrentes)

| Método | Ruta | Descripción | Auth |
|--------|------|-------------|------|
| POST | `/api/service-create` | Crear servicio | Admin |
| PUT | `/api/service-update/{serviceId}` | Actualizar servicio | Admin |
| DELETE | `/api/service-delete/{serviceId}` | Eliminar servicio | Admin |
| POST | `/api/service-report` | Reporte de servicios | Admin |

**ServiceCreateDto:**
```json
{
  "name": "Córdoba-BsAs Mañana",
  "originId": 1,
  "destinationId": 2,
  "vehicleId": 1,
  "startDay": 1,            // DayOfWeek (0=Sunday)
  "endDay": 5,
  "estimatedDuration": "04:00:00"
}
```

### Horarios de Servicio

| Método | Ruta | Descripción | Auth |
|--------|------|-------------|------|
| POST | `/api/service-schedule-add/{serviceId}` | Agregar horario | Admin |
| DELETE | `/api/service-schedule-delete/{scheduleId}` | Eliminar horario | Admin |

---

## Customer (Clientes)

| Método | Ruta | Descripción | Auth |
|--------|------|-------------|------|
| POST | `/api/customer-create` | Crear cliente | Admin |
| PUT | `/api/customer-update/{customerId}` | Actualizar cliente | Admin |
| POST | `/api/customer-report` | Reporte de clientes | Admin |

---

## City (Ciudades)

| Método | Ruta | Descripción | Auth |
|--------|------|-------------|------|
| POST | `/api/city-create` | Crear ciudad | Admin |
| PUT | `/api/city-update/{cityId}` | Actualizar ciudad | Admin |
| POST | `/api/city-report` | Reporte de ciudades | Admin |

---

## Vehicle (Vehículos)

| Método | Ruta | Descripción | Auth |
|--------|------|-------------|------|
| POST | `/api/vehicle-create` | Crear vehículo | Admin |
| PUT | `/api/vehicle-update/{vehicleId}` | Actualizar vehículo | Admin |
| POST | `/api/vehicle-report` | Reporte de vehículos | Admin |

---

## Driver (Conductores)

| Método | Ruta | Descripción | Auth |
|--------|------|-------------|------|
| POST | `/api/driver-create` | Crear conductor | Admin |
| PUT | `/api/driver-update/{driverId}` | Actualizar conductor | Admin |
| POST | `/api/driver-report` | Reporte de conductores | Admin |

---

## Auth (Autenticación)

| Método | Ruta | Descripción | Auth |
|--------|------|-------------|------|
| POST | `/api/login` | Login | [Public] |
| POST | `/api/refresh-token` | Renovar token | [Public] |
| POST | `/api/user-create` | Crear usuario | Admin |

**LoginRequestDto:**
```json
{
  "email": "user@email.com",
  "password": "password"
}
```

**Response:**
```json
{
  "token": "jwt-token",
  "refreshToken": "refresh-token",
  "expiresAt": "2025-01-20T00:00:00Z"
}
```

---

## Códigos de Respuesta

| Código | Significado |
|--------|-------------|
| 200 | Operación exitosa |
| 400 | Error de validación / Bad Request |
| 401 | No autenticado |
| 403 | No autorizado |
| 404 | Recurso no encontrado |
| 409 | Conflicto (ej: concurrencia) |
| 500 | Error interno del servidor |

---

## Estructura de Respuesta

**Éxito:**
```json
{
  "isSuccess": true,
  "value": { ... }
}
```

**Error:**
```json
{
  "isSuccess": false,
  "error": {
    "code": "Reserve.NotFound",
    "message": "La reserva no existe"
  }
}
```

---

## Paginación

Todos los endpoints `*-report` usan la misma estructura de paginación:

**Request:**
```json
{
  "pageNumber": 1,
  "pageSize": 10,
  "filter": {
    // Filtros específicos del reporte
  }
}
```

**Response:**
```json
{
  "items": [...],
  "pageNumber": 1,
  "pageSize": 10,
  "totalCount": 100,
  "totalPages": 10
}
```
