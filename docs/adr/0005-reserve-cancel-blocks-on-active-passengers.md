# Cancelar una Reserve se bloquea si tiene pasajeros activos (guard, no cascade)

Cuando se intenta pasar una `Reserve` a estado `Cancelled` (hoy vía `UpdateReserveAsync`, único camino existente), la operación se **rechaza** si la `Reserve` todavía tiene `Passenger`s en estado `PendingPayment` o `Confirmed`. El operador debe cancelar primero a cada pasajero y recién entonces puede cancelar la `Reserve`.

Elegimos un **guard** (bloqueo con error de validación) en vez de un **cascade** (cancelar la Reserve y arrastrar a todos sus pasajeros automáticamente) porque la cancelación de un `Passenger` tiene efectos por-pasajero que hoy no queremos disparar implícitamente, y porque obliga al operador a tomar la decisión explícita pasajero por pasajero.

## Considered Options

- **Cascade**: cancelar la Reserve marca a todos los pasajeros como `Cancelled` en una sola operación. Más ergonómico pero silencioso; oculta los efectos por-pasajero.
- **Guard (elegido)**: bloquear el cancel mientras existan pasajeros activos.

## Consequences

- El alcance es **estrictamente por-Reserve**: en un `IdaVuelta` (dos Reserves), cancelar una pierna no toca la otra. Esto se alinea con la ambigüedad ya documentada de "cancelación parcial no implementada" (ver `0002-no-explicit-booking-aggregate.md`).
- Estados terminales (`Traveled`, `NoShow`, `Refunded`, `Cancelled`) no bloquean, así que una Reserve cuyos pasajeros ya viajaron o fueron reembolsados sí se puede cancelar.
- El paso previo (cancelar un `Passenger`) hoy no genera refund ni ajuste de cuenta corriente; queda como deuda técnica separada.
- Si en el futuro se prefiere `cascade`, este ADR avisa que el bloqueo es deliberado y no un olvido.
