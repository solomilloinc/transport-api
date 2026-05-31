# Cambios en API: reportes de reservas (admin)

**Audiencia:** equipo / agente frontend.
**Estado:** implementado en backend, no deployado.
**Breaking change:** SÍ — el **Reporte de reservas del día** cambió la forma de la respuesta (ver §2).
**Wire format:** camelCase en toda la API (input + output), como el resto de los endpoints.

Cubre dos endpoints:
1. Reporte de reservas con pasajeros (deuda vencida) — **no breaking**, solo campo nuevo.
2. Reporte de reservas del día (marcado de "ya salió" + filtro por ruta) — **breaking** en el shape.

---

## 0. Definición compartida: "viaje ya realizado / ya salió"

Una reserva se considera **partida** cuando su datetime de salida ya pasó:

```
ReserveDate (día) + DepartureHour < ahora
```

Se combina la fecha con la hora de salida a propósito (la fecha sola no alcanza). Esta única definición se usa en los **dos** reportes: para pintar de amarillo (Reporte 2) y para acotar la deuda vencida (Reporte 1).

> **Zona horaria (resuelto):** `ReserveDate` + `DepartureHour` son **hora local de operación** (Argentina, UTC−3 fijo) y el backend compara contra el "ahora" en esa misma hora local. O sea: un viaje "sale 08:00" cambia a partido a las 08:00 **de Argentina**, no a las 08:00 UTC. El frontend no tiene que hacer ninguna conversión para `hasDeparted` ni para `overdueBalance`.

---

## 1. Reporte de reservas con pasajeros — deuda vencida

**Endpoint:** `POST passenger-reserve-report/{reserveId}` · auth `Admin`
**No breaking:** solo se agrega un campo a cada item.

### Campo nuevo: `overdueBalance`

| Campo | Tipo | Significado |
|---|---|---|
| `overdueBalance` | `number \| null` | **Deuda vencida**: saldo del cliente **solo por viajes ya realizados** (reservas ya partidas). Excluye cargos de viajes futuros. |
| `currentBalance` | `number \| null` | **Sin cambios.** Saldo total histórico de cuenta corriente (incluye futuro). |

**Acción frontend:** la columna **"debe"** debe pasar a mostrar **`overdueBalance`** en lugar de `currentBalance`.

**Por qué:** mostrar el saldo total confundía al operador, porque incluía cargos de viajes que el pasajero todavía no hizo. La deuda vencida es lo que se puede cobrar sin riesgo de error.

**Valores especiales:**
- `null` ⇒ el pasajero **no tiene cliente registrado** (no hay cuenta corriente).
- `0` ⇒ cliente registrado **sin deuda vencida**.

---

## 2. Reporte de reservas del día — "ya salió" + filtro por ruta

**Endpoint:** `POST reserve-report/{reserveDate}` (fecha en formato `yyyyMMdd`) · auth `Admin`

### 2.1 ⚠️ BREAKING — cambió la forma de la respuesta

**Antes** la respuesta era el listado paginado directo (`response.items`).
**Ahora** es un objeto que envuelve el listado + el facet de rutas:

```jsonc
{
  "reserves": {
    "items": [ /* ReserveReportItem[] */ ],
    "pageNumber": 1,
    "pageSize": 50,
    "totalRecords": 120,
    "totalPages": 3
  },
  "availableTrips": [
    { "tripId": 5, "description": "Buenos Aires - Córdoba" },
    { "tripId": 8, "description": "Buenos Aires - Rosario" }
  ]
}
```

→ El listado ahora se lee en **`response.reserves.items`** (antes `response.items`).

### 2.2 Campo nuevo en cada reserva: `hasDeparted`

Cada item de `reserves.items`:

```jsonc
{
  "reserveId": 1,
  "tripId": 5,
  "tripName": "Buenos Aires - Córdoba",
  "originName": "Buenos Aires",
  "destinationName": "Córdoba",
  "availableQuantity": 15,
  "reservedQuantity": 8,
  "departureHour": "08:00",   // string "HH:mm"
  "vehicleId": 3,
  "driverId": 2,
  "reserveDate": "2026-05-30T00:00:00",
  "hasDeparted": true          // 👈 NUEVO
}
```

**Acción frontend:** pintar la fila de **amarillo** cuando `hasDeparted === true` (el viaje ya salió). Es un cálculo automático por hora; no requiere ninguna acción del operador.

### 2.3 Filtro por ruta (Select por Travel/Viaje)

El body del request cambió a un filtro propio y opcional:

```jsonc
{
  "pageNumber": 1,
  "pageSize": 50,
  "sortBy": "reservedate",        // o "serviceorigin" | "servicedest"
  "sortDescending": false,
  "filters": { "tripId": 5 }       // opcional
}
```

- `filters.tripId` con valor ⇒ solo las reservas de **esa ruta** (Trip).
- `filters.tripId` `null` / `0` / `filters` omitido ⇒ **todas** las reservas del día.
- El filtro viejo (`tripType`, `passengers`, `departureDate`, `returnDate`, `pickupDirectionId`) **ya no aplica** a este endpoint (eso es del buscador público `public/reserve-summary`).

> "Ruta" / "Travel" / "Viaje" en la UI = **`Trip`** en el backend; el id es **`tripId`** (no "travelId").

### 2.4 `availableTrips` — opciones del Select

Lista de rutas (`Trip`) que tienen reservas **ese día**, para poblar el Select de filtro.

- Se calcula sobre el **día completo**, sin aplicar `filters.tripId`, así las opciones del Select **no cambian** al elegir una ruta.
- Cada opción: `{ tripId, description }`.

---

## Notas de alcance / estabilidad

- **`overdueBalance` (Reporte 1):** estable.
- **Reporte 2 (shape `reserves`/`availableTrips` + filtro `tripId`):** parte de un refactor en curso en backend. **Confirmar que esté consolidado** antes de hardcodear el contrato.
