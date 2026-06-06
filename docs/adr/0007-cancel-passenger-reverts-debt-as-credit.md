---
status: accepted
---

# Cancelar `Passenger` revierte la deuda como saldo a favor (sin tocar Caja, sin `Booking`)

Agregamos la operación admin de **cancelar un `Passenger` individual**, que hasta ahora no existía con manejo de plata — exactamente lo que el ADR 0005 dejó anotado como deuda técnica ("cancelar un `Passenger` hoy no genera refund ni ajuste de cuenta corriente"). La decisión central es resolverlo **sin introducir el agregado `Booking`** (ADR 0002): como los pagos (`ReservePayment`) y los cargos (`CustomerAccountTransaction`) se agregan por `(Reserve, Customer)` y no por `Passenger`, trabajamos solo sobre la cuenta corriente del cliente — un `Refund` que genera saldo a favor — sin partir ni reatribuir pagos por pasajero.

## Decisiones

- **`Status = Cancelled` (estado terminal único).** `PassengerStatusEnum.Refunded` queda **deprecado**: el "cuánto se devolvió" vive en el ledger, no en el enum. Conflar el ciclo de vida del pasajero con el desenlace financiero se rechazó por ambiguo (¿qué estado ante un reembolso parcial?).
- **Reversión de deuda (siempre):** `CustomerAccountTransaction` tipo `Refund` por `−Price`, baja `CurrentBalance` — mismo patrón que `FrequentSubscription.Cancel`. Si el pasajero no había pagado, su deuda queda en cero; si ya había pagado, `CurrentBalance` queda negativo = **saldo a favor** para futuros viajes.
- **La Caja no se toca.** Regla de negocio "caja en cero": la plata cobrada ya ingresó y no se devuelve efectivo. El `ReservePayment` original permanece intacto en su caja. No hay entidad de movimientos de caja en el modelo (la caja agrega `ReservePayment`s por `CashBoxId`), así que no se inventa una.
- **Elegibilidad:** solo `Passenger` activo (`PendingPayment` o `Confirmed`) cuya `Reserve` **no haya partido** (`ReserveDate.Date + DepartureHour > LocalNow`, ver CONTEXT.md → Reserva partida). Estados terminales y reservas ya partidas se bloquean con error accionable. Autorización Admin; envuelto en `IUnitOfWork.ExecuteInTransactionAsync`.
- **IdaVuelta: cancela ambas piernas, revierte el package una vez.** Cancelar cualquiera de las dos piernas de una persona cancela también la otra y revierte el `packagePrice` una sola vez (la inbound tiene `Price = 0`, ADR 0004). El par de una persona es la unidad de cancelación.

## Vínculo entre piernas: `Passenger.RelatedPassengerId` (fix de modelado)

El par IdaVuelta se enlazaba solo a nivel `Reserve` (`ReserveRelatedId`). Eso es ambiguo cuando **un mismo `Customer` paga varios pasajeros IdaVuelta** (ej. matrimonio): la reserva de vuelta tiene varios `Passenger` con el mismo `CustomerId` (y el mismo `DocumentNumber`, porque en el alta admin todas las piernas se cargan con los datos del pagador) apuntando a la misma `ReserveRelatedId`. Emparejar "la otra pierna" por `CustomerId`/documento podía cancelar la pierna de la persona equivocada.

Se agrega **`Passenger.RelatedPassengerId`** (FK self-ref nullable): vínculo directo pasajero↔pasajero, seteado al crear el par en los tres puntos de creación (alta admin, alta pública, frecuentes) vía navegación (los `PassengerId` no existen hasta el `SaveChanges`). `CancelPassengerAsync` usa este id para encontrar la pierna hermana, no `CustomerId`.

## Consequences

- Cierra la deuda técnica anotada en ADR 0005.
- El reembolso es **siempre saldo a favor** en cuenta corriente; nunca salida de efectivo. La Caja no se modifica. El **consumo** de ese saldo a favor queda fuera de alcance — lo gestionan los flujos de cobro existentes; esta operación solo lo deja registrado en `CurrentBalance`.
- `PassengerStatusEnum.Refunded` queda como estado muerto/deprecado.
- La `Reserve` se sigue cancelando solo cuando queda vacía de pasajeros activos (ADR 0005 intacto): cancelar pasajeros es el camino para vaciarla.
- `Passenger` gana `RelatedPassengerId` (FK self-ref + índice + migración). Se puebla de ahora en más; los pares IdaVuelta históricos quedan con `RelatedPassengerId = null` y para ellos el cancel cae al alcance por-pierna (no arrastra la hermana). Es aceptable: el dato viejo no es backfilleable sin heurística, y el bug solo afectaba el caso multi-IdaVuelta del mismo cliente.

## Considered alternatives

- **Introducir `Booking` y atribuir pagos por pasajero**: rechazado. Es el trabajo grande que 0002 difiere; cancelar se resuelve sin él.
- **Reembolso de efectivo desde la Caja (con o sin entidad `CashBoxMovement`)**: rechazado por regla de negocio ("caja en cero": la plata ya ingresó, no se devuelve efectivo). El crédito a favor cubre el caso.
- **Emparejar la pierna hermana por `CustomerId`/`DocumentNumber`**: rechazado — cancela la pierna equivocada cuando un cliente paga varios IdaVuelta. Por eso se introdujo `RelatedPassengerId`.
- **Cancelar sin tocar plata**: rechazado. Deja cargos fantasma — es justo la deuda técnica que se busca cerrar.

## Trigger to revisit

- **Mover Passenger** (reubicar a otra reserva) está **explícitamente fuera de alcance** por ahora, por pedido del negocio. Cuando se retome, `RelatedPassengerId` ya deja las piernas emparejadas sin heurística.
- Si aparece **cancelación asimétrica de IdaVuelta** (una sola pierna de un package, con reparto de revenue por leg) o un **reembolso real de efectivo/MercadoPago** (devolver plata, no solo saldo a favor), la atribución por-pasajero deja de ser evitable y corresponde introducir `Booking` (supersede 0002 y este ADR).
