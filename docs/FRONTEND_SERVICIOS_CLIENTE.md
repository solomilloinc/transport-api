# Pasajeros Frecuentes — Guía Frontend

> **Cambio importante** — Mayo 2026. El antiguo flujo `customer-create` con campo `serviceIds[]` **fue removido**. La asociación Customer⇄Service ahora vive en su propio CRUD bajo `FrequentSubscription`. Esto habilita IdaVuelta, pickup/dropoff por leg, vigencia con fechas, y la auto-creación de Passengers cuando el batch genera Reserves. Decisiones documentadas en [ADR 0004](adr/0004-frequent-passenger-subscription.md).

---

## Resumen del feature

Una `FrequentSubscription` representa la rutina de un `Customer` sobre uno o dos `Service` (slots semanales recurrentes):

- **Ida**: el cliente viaja regularmente sobre un único `Service`.
- **IdaVuelta**: el cliente viaja sobre **dos** `Service` apareados (típicamente mismo día, ej. Lun 08:00 Cba→BsAs + Lun 18:00 BsAs→Cba). Si caen el mismo día calendario aplica el precio promocional `IdaVuelta`; si caen días distintos cada leg se cobra como Ida con su propio precio (depende del tenant config `RoundTripRequiresSameDay`).

Cuando el batch `GenerateFutureReservesFunction` corre:
1. Genera Reserves para los próximos N días (existente).
2. **Para cada FrequentSubscription activa, auto-crea Passengers en estado `PendingPayment` sobre las Reserves recién generadas + carga el snapshot de precio a `Customer.CurrentBalance` vía `CustomerAccountTransaction` (tipo `Charge`). Los Passengers aparecen como deuda en el flow normal de saldar pagos; cuando el cliente paga vía el endpoint estándar, flipa a `Confirmed` igual que cualquier otra reserva admin sin pago.**

Cancelar la suscripción dispara **cascade**: cancela todos los Passengers futuros no-viajados y revierte los cargos correspondientes (Refunds).

---

## Endpoints

Todos requieren `Authorization: Bearer {token}` con rol Admin.

### Crear suscripción

```
POST /api/frequent-subscription-create
```

**Request body**:
```json
{
  "customerId": 1,
  "reserveTypeId": 1,
  "outboundServiceId": 10,
  "inboundServiceId": null,
  "outboundPickupLocationId": 100,
  "outboundDropoffLocationId": 101,
  "inboundPickupLocationId": null,
  "inboundDropoffLocationId": null,
  "startDate": "2026-05-20",
  "endDate": null
}
```

Campos:
- `reserveTypeId`: `1 = Ida`, `2 = IdaVuelta`.
- Si `reserveTypeId == 2` (IdaVuelta), **son requeridos** `inboundServiceId`, `inboundPickupLocationId`, `inboundDropoffLocationId`. Si `reserveTypeId == 1` (Ida), todos los `inbound*` deben ser `null`.
- `startDate` opcional (default = hoy del servidor). `endDate` opcional (null = indefinida). Formato `YYYY-MM-DD`.
- Pickup/Dropoff deben pertenecer a `Service.AllowedDirections` del Service respectivo (filtrar el dropdown en el frontend para no permitir inválidas).

**Response 200**: `int` (el `frequentSubscriptionId` creado).

### Editar suscripción

```
PUT /api/frequent-subscription-update/{id}
```

**Request body** (sólo campos editables):
```json
{
  "outboundPickupLocationId": 100,
  "outboundDropoffLocationId": 101,
  "inboundPickupLocationId": 102,
  "inboundDropoffLocationId": 103,
  "startDate": "2026-06-01",
  "endDate": "2026-12-31"
}
```

- **Inmutables** (cambiar = cancelar + crear nueva): `customerId`, `outboundServiceId`, `inboundServiceId`, `reserveTypeId`.
- `startDate` editable **sólo si la suscripción todavía no comenzó** (`StartDate > today`). Sino → 400 `CannotChangeStartDateAlreadyStarted`.
- Cambios de pickup/dropoff **no son retroactivos**: afectan sólo a los Passengers que se generen de ahí en adelante. Los Passengers ya creados mantienen su snapshot.

### Preview del cancel (read-only)

```
GET /api/frequent-subscription/{id}/cancel-preview
```

Read-only, idempotente, sin side effects. Devuelve los números exactos para poblar el modal de confirmación.

**Response 200**:
```json
{
  "frequentSubscriptionId": 42,
  "passengersToCancel": 4,
  "totalRefundAmount": 12500.00
}
```

- `passengersToCancel`: Passengers de esta suscripción con `ReserveDate >= ahora`, `HasTraveled = false` y `Status NOT IN (Cancelled, Traveled)`.
- `totalRefundAmount`: suma de `Passenger.Price` sobre el conjunto anterior (lo que se va a reembolsar como `CustomerAccountTransaction` Refund si se confirma el cancel).
- Si no hay nada que cancelar, devuelve `0` y `0` (no falla).

**Errores**:
- 404 `FrequentSubscription.NotFound` — id inexistente.
- 409 `FrequentSubscription.AlreadyCancelled` — la suscripción ya no está activa.

> **Nota sobre moneda**: el sistema no trackea moneda hoy. Si el frontend asume una sola por tenant, mostrarla a partir de la config del tenant; sino dejarla implícita en el texto del modal.

### Cancelar suscripción (cascade)

```
DELETE /api/frequent-subscription-cancel/{id}
```

Cancela la suscripción + cascade sobre Passengers futuros + reverso de cargos. Operación atómica. Mismas reglas de selección que `cancel-preview`.

**Errores**:
- 404 `FrequentSubscription.NotFound`
- 409 `FrequentSubscription.AlreadyCancelled` — ya estaba cancelada/borrada. (Garantiza idempotencia segura: si el frontend llama 2 veces, el segundo falla limpio.)

**UI sugerida**: invocar primero `cancel-preview`, mostrar números concretos en el modal, y al confirmar llamar al `cancel`. Ejemplo de copy:
*"Esto cancelará {passengersToCancel} reservas futuras de {cliente} y reembolsará ${totalRefundAmount} a su cuenta corriente. ¿Confirmar?"*

### Detalle por ID

```
GET /api/frequent-subscription/{id}
```

**Response 200**: `FrequentSubscriptionResponseDto` con todos los campos planos + nombres legibles (`customerFullName`, `outboundServiceName`, `inboundServiceName?`, `outboundPickupLocationName`, etc.).

### Listado paginado / reporte

```
POST /api/frequent-subscription-report
```

**Request body**:
```json
{
  "pageNumber": 1,
  "pageSize": 20,
  "sortBy": "startdate",
  "sortDescending": true,
  "filters": {
    "customerId": null,
    "outboundServiceId": null,
    "inboundServiceId": null,
    "reserveTypeId": null,
    "status": null,
    "activeAtDate": null
  }
}
```

Filtros:
- `status`: si se omite, devuelve sólo `Active`.
- `activeAtDate`: filtra suscripciones cuya vigencia incluye la fecha dada (`StartDate <= date AND (EndDate IS NULL OR EndDate >= date)`).

Sort keys disponibles: `customerid`, `outboundserviceid`, `startdate`, `status`.

---

## Errores que el frontend tiene que renderear

| Error.Code | HTTP | Mensaje sugerido para el usuario |
|---|---|---|
| `FrequentSubscription.InvalidIdaConfig` | 400 | "Para una suscripción Ida no se debe especificar Service ni datos de Vuelta." |
| `FrequentSubscription.InvalidIdaVueltaConfig` | 400 | "Para IdaVuelta se requieren Service de Vuelta y pickup/dropoff." |
| `FrequentSubscription.InvalidDateRange` | 400 | "La fecha de fin no puede ser anterior a la de inicio." |
| `FrequentSubscription.DirectionNotAllowedForService` | 400 | "El pickup o dropoff elegido no está habilitado para este servicio." (incluye `details.leg` + `details.kind` — ver abajo) |
| `FrequentSubscription.AlreadyCancelled` | 409 | "La suscripción ya no está activa." |
| `FrequentSubscription.CannotChangeStartDateAlreadyStarted` | 400 | "No se puede cambiar la fecha de inicio de una suscripción que ya comenzó." |
| `FrequentSubscription.OverlapWithExistingSubscription` | 409 | "El cliente ya tiene una suscripción activa para este servicio." |
| `FrequentSubscription.CapacityExceeded` | 409 | "No hay cupo: ya alcanzaste el máximo de suscripciones para este servicio (capacidad del vehículo)." |
| `Customer.HasActiveSubscriptions` | 409 | "No se puede eliminar el cliente: tiene suscripciones activas. Cancelálas primero." |
| `Service.HasActiveSubscriptions` | 409 | "No se puede desactivar/eliminar el servicio: tiene suscripciones activas." |
| `Service.VehicleCapacityBelowSubscriptions` | 409 | "El vehículo elegido tiene capacidad insuficiente para las suscripciones existentes." |

### Detalle estructurado de errores (`details`)

El envelope `ProblemDetails` puede incluir un campo opcional `details` (objeto) con metadata adicional para el frontend. Hoy lo usa `DirectionNotAllowedForService`:

```json
{
  "title": "FrequentSubscription.DirectionNotAllowedForService",
  "detail": "La direction 999 no está habilitada en el service 10.",
  "status": 400,
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "details": {
    "leg": "outbound",     // "outbound" | "inbound"
    "kind": "pickup",      // "pickup" | "dropoff"
    "serviceId": 10,
    "directionId": 999
  }
}
```

Con esto el frontend puede subrayar el dropdown específico (`outboundPickupLocationId`, `outboundDropoffLocationId`, `inboundPickupLocationId` o `inboundDropoffLocationId`) en vez de mostrar el error a nivel form. **Backward-compat**: si `details` falta en una respuesta vieja o futura, el frontend debería fallback a su comportamiento form-level.

---

## Cambios en endpoints existentes

### `POST /api/customer-create` y `PUT /api/customer-update/{id}`

**Eliminado**: el campo `serviceIds: number[]` ya no existe. Cualquier código que lo enviaba debe removerlo del payload — el backend lo va a ignorar como propiedad desconocida o, según el JSON parser, podría fallar. Quitar.

Los demás campos del Customer no cambian.

### `POST /api/customer-report`

El response `CustomerReportResponseDto` ya **no incluye** el campo `services`. Si la grilla de clientes mostraba esa columna, hay que removerla. El equivalente ahora se consulta vía `POST /api/frequent-subscription-report?filters.customerId=X`.

### `GET /api/services-list`

**Cambio de shape (aditivo, no-breaking)**. Ahora cada item incluye toda la metadata para poblar el form de FrequentSubscription sin tener que pedir el reporte paginado:

```json
[
  {
    "serviceId": 1,
    "name": "Lobos-CABA Lun 08:00",
    "tripId": 1,
    "tripDescription": "Lobos a CABA",
    "originCityId": 1,
    "destinationCityId": 2,
    "dayOfWeek": 1,
    "dayOfWeekName": "Lunes",
    "departureHour": "08:00:00",
    "allowedDirectionIds": [10, 11, 12]
  }
]
```

**Uso en el form de FrequentSubscription:**

- **Outbound dropdown**: etiquetar `"{tripDescription} · {dayOfWeekName} {departureHour}"` (ej. *"Lobos a CABA · Lunes 08:00"*).
- **Inbound dropdown** (sólo si IdaVuelta): filtrar a Services donde `originCityId == outbound.destinationCityId AND destinationCityId == outbound.originCityId` (trip inverso). Bonus: priorizar los que coinciden con `outbound.dayOfWeek` (mismo día = IdaVuelta promo aplica).
- **Pickup/Dropoff dropdowns**: cruzar con la lista global de Directions y filtrar a las que están en `allowedDirectionIds` del Service elegido. Si el array está vacío, todas las Directions son válidas para ese Service.

**Importante**: cada item corresponde a **un slot semanal individual** (post-refactor de Service). Donde antes había 1 Service "Cba-BsAs L-V", ahora hay 5 Services (uno por día).

### `POST /api/passenger-reserve-report/{reserveId}`

**Cambio de shape (aditivo, no-breaking)**. El `PassengerReserveReportResponseDto` ahora incluye `frequentSubscriptionId`:

```json
{
  "passengerId": 100,
  "customerId": 5,
  "frequentSubscriptionId": 42,   // ← NUEVO. null si vino manual/externo, número si vino del batch de pasajeros frecuentes
  "fullName": "María Pérez",
  ...
}
```

**Uso**: cuando `frequentSubscriptionId != null`, el frontend puede:
- Renderear un badge "FRECUENTE" en la fila del passenger.
- Linkear "Ver suscripción" a `/admin/frequent-subscriptions/{frequentSubscriptionId}`.
- Filtrar/agrupar la lista por origen.

---

## Pantalla sugerida (mockup en texto)

**Listado** (default ordering: `startdate desc`):

| Cliente | Tipo | Servicio Ida | Servicio Vuelta | Pickup Ida | Dropoff Ida | Vigencia | Status | Acciones |
|---|---|---|---|---|---|---|---|---|
| María Pérez | IdaVuelta | Cba-BsAs Lun 08:00 | BsAs-Cba Lun 18:00 | Terminal A | Retiro | 2026-05-20 → ∞ | Active | Editar / Cancelar |

**Form de creación**:
1. Customer (dropdown buscable)
2. Tipo de Reserva (radio: Ida / IdaVuelta)
3. Servicio Ida (dropdown filtrado Active, con etiqueta `{name} — {day} {hour}`)
4. Servicio Vuelta (visible sólo si IdaVuelta)
5. Pickup Ida + Dropoff Ida (dropdowns Direction filtrados por AllowedDirections del Servicio Ida)
6. Pickup Vuelta + Dropoff Vuelta (idem, sólo si IdaVuelta)
7. Fecha desde (date, default hoy)
8. Fecha hasta (date opcional)

**Form de edición**: mismos campos pero **inmutables greyed-out** (Customer, ReserveType, Services). Tooltip: *"Para cambiar, cancelá la suscripción y creá una nueva."*

**Botón Cancelar**: modal de confirmación con descripción del cascade.

---

## Notas de operación

- El batch `GenerateFutureReservesFunction` corre 1 vez por día. Una suscripción nueva queda visible en la próxima corrida — no en tiempo real. Si el admin necesita ver el efecto inmediato, puede invocar el batch manualmente vía `POST /GenerateFutureReservesFunction` (autorización Admin/SuperAdmin).
- La auto-creación es idempotente: si la sub o el batch corren dos veces, no se duplican Passengers (chequea por `(ReserveId, CustomerId, FrequentSubscriptionId)`).
- El cargo a `CustomerAccountTransaction` se hace 1 vez por Passenger creado. Para IdaVuelta promo: 1 cargo único por el `packagePrice`, asociado al outbound Reserve. El `Passenger` outbound queda con `Price = packagePrice` y el inbound con `Price = 0` — la relación se ve vía `ReserveRelatedId`. El frontend que muestre el leg de vuelta puede agregar un badge tipo "Parte de IdaVuelta (Reserve {ReserveRelatedId})" para evitar confusión al ver el Price=0.
- Si por algún motivo el batch encuentra que un slot ya tiene capacidad llena (otros Passengers no-frecuentes), **skipea** silenciosamente la creación de ese Passenger frecuente — no falla el batch. El admin debe monitorearlo via reporte. (Esta es una protección defense-in-depth — el hard cap en creación debería evitar este caso.)
