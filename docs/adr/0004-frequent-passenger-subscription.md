---
status: accepted
---

# `FrequentSubscription` reemplaza a `ServiceCustomer` como agregado de Pasajeros Frecuentes

El feature "Pasajeros Frecuentes" expresa una rutina conocida: un `Customer` viaja regularmente en uno (Ida) o dos (IdaVuelta) `Service`, y queremos que el batch que genera `Reserves` futuras también genere los `Passenger` correspondientes ya confirmados, con su pickup/dropoff y su cargo a cuenta corriente. Modelamos esto introduciendo un agregado nuevo `FrequentSubscription` y **eliminando** `ServiceCustomer` — que hoy era un join genérico sin semántica. La nueva entidad mantiene `CustomerId`, `OutboundServiceId`, `InboundServiceId` (nullable), `ReserveTypeId` (`Ida` o `IdaVuelta`), pickup/dropoff `Direction` por leg, `StartDate` (requerido, default hoy), `EndDate` (nullable, indefinida) y `Status`. Elegimos esta forma porque el nombre del agregado coincide con el nombre del feature, la pantalla de admin se mapea 1-a-1 con una fila (no hay redundancia ni linkeo mutuo), y los reportes son unidireccionales sin UNIONs.

## Decisiones operativas asociadas

- **Auto-creación de `Passenger` con `Status = PendingPayment` + cargo a `CustomerAccountTransaction`.** El batch que corre después de `GenerateFutureReservesAsync` genera Passengers con el snapshot de precio (calculado con `GetPassengerPriceAsync`, respetando ADR 0001) y lo carga a la cuenta corriente del cliente. Razón: "frecuente" significa "este cliente viaja siempre y le cobramos por adelantado", **NO** "pre-pagado". El flow normal de saldar deuda (`GetCustomerPendingReservesAsync`) filtra por `Status == PendingPayment` — usar `Confirmed` los volvía invisibles para la UI de cobro, generando un estado incoherente: `Customer.CurrentBalance` con deuda positiva pero ningún Passenger "pendiente de pagar" visible. Cuando el cliente paga vía el flow estándar, el sistema flipa el Passenger a `Confirmed` exactamente igual que con un Passenger admin-creado sin pago. _Decisión revisada en sesión posterior al grilling original — el primer cut con `Confirmed` rompió la integración con el reporte de deuda._

- **IdaVuelta con `ResolveEffectiveReserveTypeAsync` reusado.** Si el tenant tiene `RoundTripRequiresSameDay = true` y los dos Services caen en `DayOfWeek` distintos (por ende fechas distintas), el batch degrada el cobro a dos Idas separadas con sus precios respectivos, no aplica el promocional. Mismo comportamiento que ya tiene el admin booking flow.

- **Convención de pricing IdaVuelta: el outbound es el "dueño" del package, el inbound queda en 0.** Cuando el batch genera un par IdaVuelta promocional, el `Passenger` outbound se persiste con `Price = packagePrice` (ej. 12K) y el `Passenger` inbound con `Price = 0`. El cargo a `CustomerAccountTransaction` se hace UNA sola vez por el `packagePrice` completo, asociado al outbound. Razón: refleja la realidad económica (el cliente pagó 12K por el package; el asiento de vuelta está incluido sin revenue adicional), mantiene la consistencia del cálculo de deuda (`Sum(p.Price) = packagePrice`, no hay double-counting), y deja el `ReserveRelatedId` como puntero canónico desde el inbound al outbound para que cualquier consumer que necesite saber "cuánto valió la vuelta" siga el link. Alternativa rechazada: 50/50 split (6K + 6K) — matemáticamente equivalente pero visualmente engañoso (un Passenger con Price=6K en una ruta donde el Ida standalone es 10K induce a pensar que se aplicó un descuento al leg cuando en realidad es parte de un package).

- **Hard cap de capacidad en creación de suscripción.** No se puede crear una `FrequentSubscription` si el conteo de subs activas del `OutboundService` (más el conteo del `InboundService` si es IdaVuelta) supera el `Vehicle.AvailableQuantity` correspondiente. Razón: el cliente nunca puede recibir "sos frecuente" para luego descubrir que no hay asiento al llegar.

- **Hard cancel con cascade.** Cancelar una `FrequentSubscription` cancela los `Passenger` futuros no-viajados generados por ella (vía el nuevo FK `Passenger.FrequentSubscriptionId`) y revierte los `CustomerAccountTransaction` Charge correspondientes (crea Transactions Refund, baja `Customer.CurrentBalance`). Razón coherente con el auto-charge: si auto-cobrás al crear, tenés que auto-refundir al cancelar; sino quedan cargos fantasma.

- **Block (no cascade) cuando upstream entities mutan.** Desactivar/borrar un `Service`, borrar un `Customer`, o bajar la capacidad del `Vehicle` de un `Service` por debajo del conteo de subs activas → todas estas operaciones **fallan** con error accionable que lista las suscripciones afectadas. El admin decide qué hacer con cada una. Razón: estos son cambios upstream con efectos financieros sobre los clientes — cascadearlos automáticamente es decisión silenciosa, y elegimos fricción consciente sobre silencio.

- **Edits no son retroactivos.** Cambiar pickup/dropoff/endDate de una suscripción afecta sólo a Passengers que se generen de ahí en adelante. Los Passengers ya generados quedan como snapshot histórico (consistente con ADR 0001). Cambiar `CustomerId`/`ServiceId`/`ReserveTypeId` es inválido — para cambiarlos hay que cancelar y crear nueva (lo cual dispara el cascade limpiamente).

## Consequences

- `ServiceCustomer` (entidad, tabla, DbSet, EF config, FK desde `Customer`) se elimina por completo. El glosario en `CONTEXT.md` pierde la referencia a `ServiceCustomer` (que ya no estaba como término dedicado) y suma `FrequentSubscription`.
- El endpoint `customer-create` deja de aceptar `serviceIds: number[]`. La asociación Customer↔Service pasa a vivir en el nuevo CRUD de `FrequentSubscription` (5 endpoints HTTP nuevos, ver plan de implementación).
- El frontend de "asociar servicios a un cliente" (descripto hoy en `docs/FRONTEND_SERVICIOS_CLIENTE.md`) se replantea: en vez de un multiselect dentro del form de Customer, es una pantalla CRUD aparte con campos por suscripción (Service Ida, Service Vuelta opcional, ReserveType, pickup/dropoff, fechas).
- `Passenger` gana `FrequentSubscriptionId int?` (FK nullable, sólo poblado en Passengers auto-creados por el batch). Habilita el cascade del cancel.
- `Service` y `Customer` ganan errores nuevos (`HasActiveSubscriptions`, `VehicleCapacityBelowSubscriptions`) y validación en sus respectivos `Update`/`Delete`.
- El batch `GenerateFutureReservesFunction` ahora orquesta dos pases: primero `IServiceBusiness.GenerateFutureReservesAsync` (existente), después `IFrequentPassengerBusiness.GenerateFrequentPassengersAsync` (nuevo). Si querés rebatchear sólo Passengers (por ejemplo, agregaste suscripciones nuevas y no querés regenerar Reserves), tenés el método aparte.

## Considered alternatives

- **Extender `ServiceCustomer` con `PairedServiceId` y `ReserveTypeId` (Opción B2 del grilling)**: rechazado por claridad. La fila resultante tenía dos roles superpuestos (join genérico + subscription specifics) y el reporte "¿quiénes están suscriptos a este Service?" necesitaba UNIONs.
- **Par mutuo en dos filas (Opción B1)**: rechazado. Invariante bidireccional propenso a corrupción si una fila se borra y la otra queda dangling.
- **Soft cancel sin cascade**: rechazado. Deja cargos fantasma y asientos ocupados de mentira — la peor combinación operativa.
- **Cascade en upstream (cancelar subs cuando se desactiva un Service)**: rechazado. Decisión silenciosa con efectos financieros — mejor el block + error accionable que fuerza al admin a decidir.
- **Status `PendingPayment` con flujo de pago manual posterior**: rechazado. Rompe la semántica de "frecuente". Si dudás del pago, no es frecuente, es reserva manual ordinaria.

## Trigger to revisit

Este ADR conviene re-evaluarse si aparece:

1. Necesidad de "pausar" suscripciones temporalmente sin cancelarlas (ej. cliente que se va de vacaciones 2 semanas). Hoy lo resolvemos con `EndDate` + crear nueva al volver, pero si el caso es frecuente se justifica un `Status = Paused`.
2. Demanda de un ratio configurable "X% capacidad para frecuentes, Y% para ad-hoc". Hoy es 100% para quien llegue primero — frecuentes consumen lo que reservan, ad-hoc usa lo que queda.
3. Cancelación parcial — un cliente cancela sólo una fecha puntual de su suscripción sin cancelar la suscripción entera. Hoy se resuelve cancelando el Passenger individual en la Reserve.
