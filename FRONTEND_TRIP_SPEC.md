# Especificación Frontend - Pantallas de Trip

## Modelo de Datos

```
Trip (Ruta comercial)
├── TripId: int
├── Description: string (ej: "Córdoba - San Juan")
├── OriginCityId: int → City
├── DestinationCityId: int → City
├── Status: Active/Inactive/Deleted
└── Prices: List<TripPrice>

TripPrice (Precio por destino/tipo)
├── TripPriceId: int
├── TripId: int → Trip
├── CityId: int → City (ciudad destino para el precio)
├── DirectionId: int? → Direction (opcional, para parada específica)
├── ReserveTypeId: int (1=Ida, 2=IdaVuelta)
├── Price: decimal
├── Order: int (orden de paradas)
└── Status: Active/Deleted
```

## Endpoints Disponibles

| Método | Ruta | Descripción |
|--------|------|-------------|
| POST | `/api/trip-create` | Crear trip |
| PUT | `/api/trip-update/{tripId}` | Actualizar trip |
| DELETE | `/api/trip-delete/{tripId}` | Eliminar trip |
| POST | `/api/trip-report` | Listado paginado |
| GET | `/api/trip/{tripId}` | Obtener trip por ID |
| POST | `/api/trip-price-add` | Agregar precio |
| PUT | `/api/trip-price-update/{tripPriceId}` | Actualizar precio |
| DELETE | `/api/trip-price-delete/{tripPriceId}` | Eliminar precio |
| PUT | `/api/trip-prices-update-percentage` | Actualización masiva % |

## DTOs

### TripCreateDto (crear/actualizar trip)
```json
{
  "description": "Córdoba - San Juan",
  "originCityId": 1,
  "destinationCityId": 2
}
```

### TripPriceCreateDto (agregar precio)
```json
{
  "tripId": 1,
  "cityId": 2,           // Ciudad destino
  "directionId": null,   // Opcional: parada específica
  "reserveTypeId": 1,    // 1=Ida, 2=IdaVuelta
  "price": 15000.00,
  "order": 1             // Orden de la parada
}
```

### TripPriceUpdateDto (actualizar precio)
```json
{
  "cityId": 2,
  "directionId": null,
  "reserveTypeId": 1,
  "price": 16000.00,
  "order": 1
}
```

### TripReportResponseDto (respuesta)
```json
{
  "tripId": 1,
  "description": "Córdoba - San Juan",
  "originCityId": 1,
  "originCityName": "Córdoba",
  "destinationCityId": 2,
  "destinationCityName": "San Juan",
  "status": "Active",
  "prices": [
    {
      "tripPriceId": 1,
      "cityId": 2,
      "cityName": "San Juan",
      "directionId": null,
      "directionName": null,
      "reserveTypeId": 1,
      "reserveTypeName": "Ida",
      "price": 15000.00,
      "order": 1,
      "status": "Active"
    }
  ]
}
```

### PriceMassiveUpdateDto (actualización masiva)
```json
{
  "priceUpdates": [
    { "reserveTypeId": 1, "percentage": 10.5 },
    { "reserveTypeId": 2, "percentage": 8.0 }
  ]
}
```

### Filtros para Reporte (TripReportFilterDto)
```json
{
  "originCityId": 1,        // Opcional
  "destinationCityId": 2,   // Opcional
  "status": "Active"        // Opcional
}
```

## Pantallas Sugeridas

### 1. Listado de Trips (`/admin/trips`)

**Componentes:**
- Tabla con columnas: Descripción, Origen, Destino, Cant. Precios, Estado, Acciones
- Filtros: Select Origen, Select Destino, Select Estado
- Paginación

**Acciones por fila:**
- Editar → `/admin/trips/{id}/edit`
- Ver/Gestionar Precios → `/admin/trips/{id}/prices`
- Eliminar (con confirmación)

### 2. Crear Trip (`/admin/trips/new`)

**Formulario:**
```
┌─────────────────────────────────────┐
│ Descripción: [________________]     │
│ Ciudad Origen: [Select ▼]           │
│ Ciudad Destino: [Select ▼]          │
│                                     │
│ [Cancelar]  [Guardar]               │
└─────────────────────────────────────┘
```

**Validaciones:**
- Descripción requerida
- Origen y Destino requeridos
- Origen ≠ Destino
- No puede existir otro Trip activo con mismo origen/destino

### 3. Editar Trip (`/admin/trips/{id}/edit`)

Mismo formulario que crear, precargado con datos.

### 4. Gestión de Precios (`/admin/trips/{id}/prices`)

**Layout:**
```
┌─────────────────────────────────────────────────────┐
│ Trip: Córdoba → San Juan                            │
├─────────────────────────────────────────────────────┤
│ [+ Agregar Precio]  Filtrar: [Tipo Reserva ▼]       │
├─────────────────────────────────────────────────────┤
│ Ciudad    │ Dirección │ Tipo      │ Precio  │ Orden │ │
│───────────┼───────────┼───────────┼─────────┼───────┤ │
│ San Juan  │ -         │ Ida       │ $15,000 │ 1     │⋮│
│ San Juan  │ -         │ IdaVuelta │ $28,000 │ 1     │⋮│
│ Chepes    │ Ruta 38   │ Ida       │ $8,000  │ 2     │⋮│
└─────────────────────────────────────────────────────┘
```

**Modal Agregar/Editar Precio:**
```
┌─────────────────────────────────────┐
│ Ciudad: [Select ▼]                  │
│ Dirección: [Select ▼] (opcional)    │
│ Tipo Reserva: [Select ▼]            │
│ Precio: [$_______]                  │
│ Orden: [__]                         │
│                                     │
│ [Cancelar]  [Guardar]               │
└─────────────────────────────────────┘
```

### 5. Actualización Masiva de Precios (`/admin/trips/prices/bulk-update`)

**Layout:**
```
┌─────────────────────────────────────────────────────┐
│ Actualización Masiva de Precios                     │
├─────────────────────────────────────────────────────┤
│ Esta acción actualizará TODOS los precios activos   │
│ según el porcentaje indicado por tipo de reserva.   │
├─────────────────────────────────────────────────────┤
│ Tipo Reserva    │ Porcentaje (%)                    │
│─────────────────┼───────────────────────────────────│
│ Ida             │ [____] %                          │
│ Ida y Vuelta    │ [____] %                          │
├─────────────────────────────────────────────────────┤
│ Ejemplo: 10% sobre $15,000 = $16,500                │
│ Usar valores negativos para descuentos (-5%)        │
│                                     │
│ [Cancelar]  [Aplicar Cambios]                       │
└─────────────────────────────────────────────────────┘
```

## Relación con Service (IMPORTANTE)

Al crear/editar un **Service**, ahora es obligatorio seleccionar un **Trip**.

### ServiceCreateRequestDto (actualizado)
```json
{
  "name": "Servicio Mañana",
  "tripId": 1,              // NUEVO - Obligatorio
  "originId": 1,            // Parada de salida (puede diferir del Trip)
  "destinationId": 2,       // Parada de llegada (puede diferir del Trip)
  "estimatedDuration": "04:00:00",
  "vehicleId": 1,
  "schedules": [
    { "departureHour": "08:00:00", "isHoliday": false }
  ]
}
```

### Pantalla de Service actualizada

```
┌─────────────────────────────────────┐
│ Nombre: [________________]          │
│ Trip (Ruta): [Select ▼]  ← NUEVO    │
│ Parada Origen: [Select ▼]           │
│ Parada Destino: [Select ▼]          │
│ Vehículo: [Select ▼]                │
│ Duración Estimada: [__:__]          │
│ ...                                 │
└─────────────────────────────────────┘
```

**Nota:** El Trip define los precios. Las paradas origen/destino del Service son operativas (dónde sale/llega el bus físicamente).

## Tipos de Reserva (ReserveTypeId)

| ID | Nombre | Descripción |
|----|--------|-------------|
| 1 | Ida | Un tramo (A→B o B→A) |
| 2 | IdaVuelta | Viaje redondo (dos tramos) |

## Estados (EntityStatusEnum)

| Valor | Descripción |
|-------|-------------|
| Active | Activo y visible |
| Inactive | Inactivo temporalmente |
| Deleted | Eliminado (soft delete) |

## Consideraciones de UX

1. **Selects dependientes:** Al seleccionar Trip en Service, podrías precargar origen/destino del Trip como valores por defecto.

2. **Validación de precios únicos:** No puede haber dos precios activos con la misma combinación (TripId + CityId + DirectionId + ReserveTypeId).

3. **Orden de precios:** El campo `Order` indica el orden de las paradas intermedias en la ruta.

4. **Actualización masiva:** Mostrar preview de cambios antes de confirmar.
