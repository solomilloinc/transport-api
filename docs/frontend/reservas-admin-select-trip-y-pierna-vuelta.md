# Reservas admin: Select por Trip + pierna de vuelta del IdaVuelta

**Audiencia:** equipo / agente frontend.
**Estado:** backend implementado (no deployado).
**Endpoint afectado:** `POST /reserve-report/{reserveDate}` (admin). Wire format **camelCase** (input + output).

Resuelve dos cosas de la página de Reservas admin:

1. **Select por Trip** en lugar de scroll infinito de todas las reservas del día.
2. **Bug del IdaVuelta:** en la pierna de vuelta, la lista debe mostrar **solo la ruta inversa** (no ambos sentidos).

Ambas se apoyan en **un único** cambio de contrato: `reserve-report/{date}` ahora acepta un filtro opcional `tripId` y devuelve, además de la página de reservas, el listado de Trips que tienen reservas ese día.

---

## 1. Nuevo contrato de `reserve-report/{date}`

`reserveDate` sigue yendo en la URL en formato `yyyyMMdd` (ej. `20260531`).

### Request (body)

```jsonc
{
  "pageNumber": 1,
  "pageSize": 50,
  "sortBy": "reservedate",      // opcional: "reservedate" | "serviceorigin" | "servicedest"
  "sortDescending": false,
  "filters": {
    "tripId": 20                 // opcional. null/ausente/0 ⇒ TODAS las reservas del día
  }
}
```

> El filtro se achicó: antes se mandaba el `ReserveReportFilterRequestDto` gordo
> (`tripId, tripType, passengers, departureDate, returnDate, pickupDirectionId`).
> Para este endpoint **solo importa `tripId`**; el resto ya no se manda. (El DTO gordo
> sigue vivo únicamente para el endpoint agrupado público `public/reserve-summary`.)

### Response (body)

```jsonc
{
  "reserves": {                  // la página de reservas (igual que antes, pero anidada acá)
    "items": [
      {
        "reserveId": 2,
        "tripId": 20,
        "tripName": "Capital Federal → Lobos",
        "originName": "Capital Federal",
        "destinationName": "Lobos",
        "availableQuantity": 4,
        "reservedQuantity": 1,
        "departureHour": "10:00",
        "vehicleId": 7,
        "driverId": 0,
        "reserveDate": "2026-05-31T00:00:00",
        "hasDeparted": false      // flag amarillo "ya salió" (feature aparte; ignorable acá)
      }
    ],
    "pageNumber": 1,
    "pageSize": 50,
    "totalRecords": 1,
    "totalPages": 1
  },
  "availableTrips": [            // FACET para poblar el Select
    { "tripId": 10, "description": "Lobos → Capital Federal" },
    { "tripId": 20, "description": "Capital Federal → Lobos" }
  ]
}
```

**Cambio de shape (breaking):** antes la respuesta era el `PagedReportResponseDto` directo
(`items`, `pageNumber`, …) en la raíz. Ahora esa página vive bajo `reserves`, y se agrega
`availableTrips` al lado.

`availableTrips` se calcula sobre **el día completo, sin aplicar el filtro `tripId`**: las
opciones del Select no cambian cuando elegís una ruta.

---

## 2. Página de Reservas — Select por Trip

1. El usuario elige un día en el calendario → `POST /reserve-report/{day}` con `filters.tripId = null`.
2. Poblar el `<select>` con `availableTrips` (`description` como label, `tripId` como value).
   Solo aparecen rutas que **tienen reservas ese día** (no todo el catálogo).
3. Al elegir una ruta → volver a pedir con `filters.tripId = <elegido>`; `reserves.items`
   vuelve filtrado a esa ruta. Sin selección ("Todas") ⇒ `tripId = null` ⇒ comportamiento actual.

---

## 3. Alta IdaVuelta — pierna de vuelta = solo ruta inversa

Una reserva IdaVuelta del negocio son **dos Trips distintos**: la ida `A → B` y la vuelta
`B → A` (su **Trip inverso**, Origin/Destination intercambiados). El bug era que la lista de
"viajes de vuelta" traía todas las reservas del día, incluido el mismo sentido que la ida.

**Flujo correcto:**

1. Con la ida ya elegida tenés su `tripId`. **Ojo:** ni la fila del reporte
   (`ReserveReportResponseDto` trae `originName`/`destinationName`, no IDs) ni `availableTrips`
   (`{ tripId, description }`) traen los city IDs. El **único** lugar que expone
   `originCityId`/`destinationCityId` es el catálogo de Trips.
2. Cargá el catálogo de Trips desde `POST /trip-report` (admin; cada `TripReportResponseDto`
   trae `tripId`, `originCityId`, `destinationCityId`, `status`). Es la misma fuente que el
   alta ya usa para pricing.
3. Resolvé el **Trip inverso**: el `Trip` activo del catálogo con
   `originCityId === ida.destinationCityId && destinationCityId === ida.originCityId`.
4. Pedí la lista de vuelta a `POST /reserve-report/{returnDate}` con
   `filters.tripId = inverseTripId`. Te llegan **solo** las reservas de la ruta inversa.

### Regla dura (no negociable)

> La lista de la pierna de vuelta se llama **SIEMPRE** con un `tripId` inverso resuelto.

Motivo: `reserve-report/{date}` interpreta `tripId` ausente/null/0 como **"todas las reservas
del día"** (lo que necesita la página de Reservas). Si en la pierna de vuelta mandaras un
`tripId` vacío, **reaparece el bug original** (ambos sentidos en la lista).

Por eso:

- Si **no existe** Trip inverso configurado para esa ciudad-par → mostrar estado vacío
  ("No hay ruta de vuelta configurada para esta combinación") y **NO** llamar al endpoint.
- Nunca caer al endpoint sin `tripId` desde el contexto de vuelta.

(Decisión y alternativas descartadas en [docs/adr/0006-admin-return-leg-resolves-inverse-trip-client-side.md](../adr/0006-admin-return-leg-resolves-inverse-trip-client-side.md).)

---

## 4. Tipos TypeScript de referencia

```ts
interface ReserveDayReportFilter {
  tripId?: number | null;            // null/0 ⇒ todas
}

interface ReserveDayReportRequest {
  pageNumber: number;
  pageSize: number;
  sortBy?: 'reservedate' | 'serviceorigin' | 'servicedest';
  sortDescending?: boolean;
  filters: ReserveDayReportFilter;
}

interface ReserveTripOption {
  tripId: number;
  description: string;               // ej. "Capital Federal → Lobos"
}

interface ReserveReportRow {
  reserveId: number;
  tripId: number;
  tripName: string;
  originName: string;
  destinationName: string;
  availableQuantity: number;
  reservedQuantity: number;
  departureHour: string;             // "HH:mm"
  vehicleId: number;
  driverId: number;
  reserveDate: string;               // ISO
  hasDeparted: boolean;
}

interface PagedReportResponse<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalRecords: number;
  totalPages: number;
}

interface ReserveDayReportResponse {
  reserves: PagedReportResponse<ReserveReportRow>;
  availableTrips: ReserveTripOption[];
}
```

---

## 5. Checklist frontend

- [ ] Migrar el request de `reserve-report/{date}` al filtro slim (`{ tripId }`).
- [ ] Leer la respuesta desde `reserves.items` (antes era la raíz) + usar `availableTrips`.
- [ ] Página de Reservas: poblar el Select con `availableTrips`; "Todas" ⇒ `tripId = null`.
- [ ] Alta IdaVuelta: resolver el `inverseTripId` y mandarlo SIEMPRE en la pierna de vuelta.
- [ ] Sin Trip inverso ⇒ estado vacío, sin llamar al endpoint.
