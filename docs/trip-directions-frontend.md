# TripPickupStop: Horarios de Subida por Parada

## Resumen

Se agregó la entidad `TripPickupStop` que permite definir **paradas intermedias** en un Trip con un `PickupTimeOffset` (tiempo desde la hora de partida). Esto permite mostrar al usuario el horario exacto de subida en cada parada.

---

## Nuevos Endpoints

### POST `/trip-pickup-stop-create`
Crea una parada de subida en un Trip.

**Request:**
```json
{
  "tripId": 1,
  "directionId": 10,
  "order": 0,
  "pickupTimeOffset": "00:30:00"
}
```

**Response:** `int` (TripPickupStopId creado)

### PUT `/trip-pickup-stop-update/{tripPickupStopId}`
Actualiza una parada existente.

**Request:**
```json
{
  "directionId": 10,
  "order": 1,
  "pickupTimeOffset": "00:45:00"
}
```

### DELETE `/trip-pickup-stop-delete/{tripPickupStopId}`
Elimina (soft delete) una parada.

> Todos requieren rol **Admin**.

---

## Cambios en DTOs Existentes

### `GET /trip/{tripId}` — TripReportResponseDto

Se agregan dos campos nuevos:

```typescript
interface TripReportResponseDto {
  // ... campos existentes sin cambios ...

  // NUEVO: lista de paradas con offset de tiempo
  stopSchedules: TripPickupStopReportDto[] | null;
}

interface TripPickupStopReportDto {
  tripPickupStopId: number;
  directionId: number;
  directionName: string;
  cityId: number;
  cityName: string;
  order: number;
  pickupTimeOffset: string; // formato "HH:mm:ss"
}
```

Además, `PickupOptionDto` ahora incluye el offset:

```typescript
interface PickupOptionDto {
  directionId: number;
  displayName: string;
  pickupTimeOffset: string | null; // NUEVO — formato "HH:mm:ss", null si no tiene offset configurado
}
```

### `POST /reserve-report` — Búsqueda pública de reservas

#### Request — ReserveReportFilterRequestDto

Nuevo campo opcional `pickupDirectionId`:

```typescript
interface ReserveReportFilterRequestDto {
  tripId: number;
  tripType: string;
  passengers: number;
  departureDate: string;
  returnDate?: string;
  pickupDirectionId?: number; // NUEVO — opcional, filtra y calcula horarios por parada
}
```

Cuando se envía `pickupDirectionId`:
- Se valida que la dirección exista como `TripPickupStop` activa del Trip.
- Cada reserva en la respuesta incluye `stopSchedules` con los horarios calculados.

#### Response — ReserveExternalReportResponseDto

```typescript
interface ReserveExternalReportResponseDto {
  reserveId: number;
  originName: string;
  destinationName: string;
  departureHour: string;
  departureDate: string;
  estimatedDuration: string;
  arrivalHour: string;
  price: number;
  availableQuantity: number;
  vehicleName: string;
  tripId: number;
  stopSchedules: ReserveStopScheduleDto[] | null; // NUEVO
}

interface ReserveStopScheduleDto {
  directionId: number;
  directionName: string;
  order: number;
  pickupTime: string; // formato "HH:mm" — hora calculada = departureHour + pickupTimeOffset
}
```

---

## Cálculo de Horarios

El horario de subida en cada parada se calcula como:

```
pickupTime = reserva.departureHour + tripPickupStop.pickupTimeOffset
```

**Ejemplo:**
- Reserva sale a las `08:00`
- TripPickupStop "Parada Centro" tiene offset `00:30:00`
- TripPickupStop "Parada Norte" tiene offset `01:00:00`

Resultado en `stopSchedules`:
```json
[
  { "directionId": 10, "directionName": "Parada Centro", "order": 0, "pickupTime": "08:30" },
  { "directionId": 20, "directionName": "Parada Norte", "order": 1, "pickupTime": "09:00" }
]
```

---

## Flujo Sugerido para el Frontend

### Admin — Configuración de paradas

1. En el ABM de Trip, agregar sección "Paradas" debajo de precios.
2. Usar `GET /trip/{tripId}` → `stopSchedules` para listar paradas existentes.
3. CRUD con los 3 endpoints nuevos (`trip-pickup-stop-create/update/delete`).
4. Campos del formulario:
   - **Dirección** (combo de direcciones de la ciudad origen)
   - **Orden** (numérico, para ordenar las paradas)
   - **PickupTimeOffset** (input de tiempo, ej: `00:30`)

### Público — Búsqueda de reservas

1. En el combo de "Punto de subida", usar `pickupOptions` de `GET /trip/{tripId}`.
   - Ahora cada opción incluye `pickupTimeOffset` para mostrar referencia al usuario.
2. Al buscar reservas con `POST /reserve-report`, enviar `pickupDirectionId` con la dirección seleccionada.
3. En los resultados, mostrar `stopSchedules` de cada reserva para que el usuario vea el horario de subida en su parada.

---

## Notas

- `pickupTimeOffset` se recibe como `TimeSpan` en formato `"HH:mm:ss"` (ej: `"00:30:00"` = 30 minutos).
- `pickupTime` en `stopSchedules` se devuelve como `string` en formato `"HH:mm"` (ej: `"08:30"`).
- Si no se envía `pickupDirectionId` en la búsqueda, `stopSchedules` será `null` (comportamiento retrocompatible).
- Los `stopSchedules` en `GET /trip/{tripId}` siempre se devuelven si existen direcciones activas.
