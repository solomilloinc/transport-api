# Transport API — Domain Context

Glossary canónico del dominio de transporte de pasajeros. Define el lenguaje ubicuo del sistema y resuelve las ambigüedades terminológicas que aparecen entre el código, `DOMAIN.md`, y el lenguaje del negocio.

## Language

### Rutas y precios

**Trip**:
Ruta direccional entre dos ciudades con sus precios asociados. Es una definición abstracta, no un viaje físico.
_Avoid_: Route, Itinerary, Viaje.

**TripPrice**:
Precio para un tramo de un `Trip`, identificado por ciudad-destino, opcional `Direction`, y `ReserveType`. Es la fuente de verdad del precio al momento de crear un `Passenger`.
_Avoid_: Tarifa, Fare, Rate.

**Direction**:
Parada intermedia o específica dentro de una ciudad que permite precios diferenciados. Pickup/dropoff a nivel calle, no a nivel ciudad.
_Avoid_: Stop, Address, Parada.

**ReserveType**:
Clasificación del tramo desde la perspectiva del pasajero: `Ida`, `Vuelta`, o `IdaVuelta`. Determina qué `TripPrice` aplica.
_Avoid_: Direction (ya tomado), TripDirection, Sense.

### Capacidad y horarios

**Reserve**:
Espacio reservado para una fecha, horario y vehículo específicos. Es un **contenedor de pasajeros**, no se cobra en sí mismo.
_Avoid_: Reservation, Booking, Slot.

**Service**:
Slot semanal recurrente (un `Trip`, un `DayOfWeek`, una `DepartureHour`) que genera una `Reserve` por semana para los próximos N días. No tiene precio (los precios viven en `Trip`/`TripPrice`).
_Avoid_: Schedule, Route, Línea, Plantilla.

**Vehicle**:
Unidad física con capacidad fija (`AvailableQuantity`). Define el techo de pasajeros de cada `Reserve`.
_Avoid_: Bus, Van, Unit.

### Compra y cobro

**Passenger**:
Pasajero individual asociado a una `Reserve` con su `Price` snapshot. Hoy es la **unidad de cobro** del sistema.
_Avoid_: Ticket, Boleto, Pasaje, Seat.

**Customer**:
Cliente registrado en el sistema, opcionalmente referenciado por uno o más `Passenger` y con cuenta corriente (`CustomerAccountTransaction`).
_Avoid_: User (eso es de auth), Client, Account.

**FrequentSubscription**:
Suscripción de un `Customer` a un slot recurrente (`Service` de Ida y, opcionalmente, `Service` de Vuelta como `IdaVuelta`). Captura pickup/dropoff `Direction` por leg y un rango de vigencia (`StartDate` requerido, `EndDate` opcional). Cada vez que el batch genera una `Reserve` para uno de sus Services en la ventana, crea un `Passenger` confirmado con el snapshot de precio + cargo automático a la cuenta corriente del cliente.
_Avoid_: ServiceCustomer (concepto previo, eliminado), Membership, Recurring booking.

**ReservePayment**:
Registro de cobro asociado a una o más `Passenger` de una `Reserve`. Incluye estado (`Pending`/`Confirmed`/`Failed`) y referencia al gateway externo (MercadoPago).
_Avoid_: Payment (genérico), Charge, Transaction (eso es de cuenta corriente).

**ReserveSlotLock**:
Bloqueo temporal de cupos durante el flujo de pago externo. Expira automáticamente; previene overbooking.
_Avoid_: Hold, Reservation lock, Cart.

---

## Relationships

- Una **Trip** tiene una o más **TripPrices** (una por ciudad-destino × `ReserveType` × `Direction` opcional).
- Una **Reserve** referencia exactamente una **Trip** (para pricing) y exactamente un **Vehicle** (para capacidad).
- Una **Reserve** contiene cero o más **Passengers**.
- Un **Service** genera una **Reserve** por semana (vía batch diario que mira hacia adelante N días) para el `DayOfWeek` + `DepartureHour` que define.
- Un **Customer** tiene cero o más **FrequentSubscription**. Cada una referencia 1 `Service` (Outbound) y, opcionalmente, otro `Service` (Inbound) si el tipo es `IdaVuelta`.
- Una **FrequentSubscription** activa fuerza, en cada corrida del batch, la creación de un `Passenger` confirmado por cada `Reserve` generada en sus `Service(s)` dentro de la vigencia.
- Un **Passenger** referencia opcionalmente un **Customer**.
- Un **Passenger** referencia opcionalmente otro **Passenger** (via `ReserveRelatedId`) cuando es `IdaVuelta`.
- Una **ReservePayment** referencia exactamente una **Reserve**.

### IdaVuelta en concreto

Un "pasaje IdaVuelta" del negocio se modela como **dos Reserves** (la de ida y la de vuelta) y, por cada persona viajando, **dos Passengers** linkeados entre sí por `ReserveRelatedId`. No existe una entidad única que represente la compra IdaVuelta — la unión es implícita.

---

## Example dialogue

> **Dev:** "Si una familia de 4 compra IdaVuelta, ¿cuántas Reserves se crean?"
> **Negocio:** "Dos. Una para el viaje de ida y otra para el de vuelta."
> **Dev:** "¿Y los pasajeros?"
> **Negocio:** "Cuatro en cada Reserve, ocho en total. Cada persona aparece dos veces, una en la ida y otra en la vuelta. Internamente quedan emparejados con `ReserveRelatedId`."
> **Dev:** "¿Y la compra? ¿Cómo la veo como una unidad?"
> **Negocio:** "Por ahora no se ve como unidad. Tenés un `ReservePayment` que cobra los 8 pasajeros juntos, pero no existe un 'Booking' que agrupe las dos Reserves. Si querés cancelar 'la mitad de un IdaVuelta' hoy no hay un flujo claro."
> **Dev:** "Ok, entonces para el sistema actual la unidad de cobro es **Passenger**, no Reserve ni Booking."
> **Negocio:** "Exacto."

---

## Flagged ambiguities

### 1. Unidad de venta: `Passenger` vs `Reserve` vs "Booking" (no existe)

El sistema cobra y modela el precio por `Passenger`, pero no existe un concepto `Booking`/`Compra` que agrupe la unidad de venta real (varios `Passenger` + 1–2 `Reserve` + 1 `ReservePayment`). La cancelación parcial — por ejemplo, una de dos personas de una familia cancela solo su vuelta — **no está implementada** ni decidida. Cuando llegue ese requirement habrá que decidir si introducir el agregado `Booking` o seguir trabajando a nivel `Passenger`.

**Resolución actual:** la unidad de cobro es `Passenger`. Documentado en `docs/adr/0002-no-explicit-booking-aggregate.md`.

### 2. Fuente de capacidad: `Vehicle.AvailableQuantity` vs `Service`

La capacidad efectiva de una `Reserve` surge del `Vehicle` asignado (el `Service` ya no aporta capacidad propia tras el refactor de Mayo 2026). Hoy esa lógica está distribuida en múltiples puntos del código (`ReserveBusiness.LockReserveSlots`, validaciones inline en `CreatePassengerReserves`) y no hay un único punto de verdad. Es uno de los motivos para extraer `IReserveCapacityValidator` durante el deepening.

**Resolución actual:** sin resolver semánticamente; pendiente de consolidación técnica en el refactor de deep modules.

### 3. `Passenger.Price`: snapshot histórico vs derivado

¿`Passenger.Price` es un cache de `TripPrice.Price` o un snapshot congelado al momento de la compra? El código permite ambas interpretaciones porque acepta el `Price` desde el request pero también lo recalcula desde `TripPrice` cuando falta.

**Resolución actual:** snapshot histórico. Documentado en `docs/adr/0001-passenger-price-is-historical-snapshot.md`.
