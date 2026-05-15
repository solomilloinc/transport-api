---
status: accepted
---

# No existe un agregado `Booking`; la unidad de cobro es `Passenger`

El sistema modela una "compra" implícitamente: una operación de venta puede crear 1–2 `Reserve` (una sola en `Ida`/`Vuelta`, dos en `IdaVuelta`), 1+ `Passenger`, y 1 `ReservePayment` que las cobra juntas, pero no existe ninguna entidad explícita `Booking` o `Purchase` que las agrupe. La unidad de cobro y la unidad de cancelación efectivas del sistema son el `Passenger`, no la compra. Aceptamos esta no-decisión como decisión consciente: introducir `Booking` hoy sería un agregado sin caso de uso (la cancelación parcial — ej. una persona cancela solo su vuelta — no está implementada ni requerida todavía).

## Consequences

- El `IReservePaymentOrchestrator` que se introducirá en el refactor de deep modules orquesta el pago **a nivel `ReservePayment` + lista de `Passenger`**, no a nivel `Booking`.
- Los eventos del outbox viajan con `ReserveId` + `PassengerId[]`, no con `BookingId`.
- Cualquier reporting que necesite "ver la compra completa" (ej. una vista de detalle de la venta IdaVuelta de una familia) tiene que reconstruirla cruzando `ReservePayment` con los `Passenger` de las dos `Reserve` linkeadas por `ReserveRelatedId`.

## Trigger to revisit

Este ADR debe re-evaluarse cuando aparezca **cualquiera** de estos requirements:

1. Cancelación parcial de una compra IdaVuelta (un pasajero cancela solo ida o solo vuelta, los demás mantienen ambos tramos).
2. Reembolsos asimétricos (un pago cubre 4 pasajeros, hay que reembolsar a 1 con monto distinto al pro-rata).
3. Modificación de la compra como unidad (cambiar fecha de la compra entera afectando ambas Reserves).
4. Reporting que requiera "una fila por compra" con todos los pasajeros y ambas reserves agrupadas.

Si llega cualquiera de los cuatro, introducir el agregado `Booking` deja de ser un costo gratuito y pasa a ser la solución correcta. En ese momento se supersede este ADR.

## Considered alternatives

- **Introducir `Booking` ahora**: rechazado. Agrega una tabla, una migration, refactor del outbox y de los flujos de pago, sin ningún consumidor actual del concepto.
- **Tratar `Reserve` como unidad de compra**: rechazado. No funciona para `IdaVuelta` (donde una compra atraviesa dos `Reserve`) y oculta el hecho de que el cobro es por `Passenger`.
