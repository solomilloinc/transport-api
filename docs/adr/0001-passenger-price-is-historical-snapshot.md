---
status: accepted
---

# `Passenger.Price` es snapshot histórico, no derivado de `TripPrice`

Cuando se crea un `Passenger`, su `Price` se rellena con el valor vigente de `TripPrice.Price` en ese instante (resuelto vía la prioridad `Direction → CityId destino → ReserveType`) y queda **congelado** a partir de ahí. Si `TripPrice.Price` cambia más tarde, los `Passenger` existentes mantienen su precio original — el cambio aplica solo a `Passenger` creados después. Elegimos esta semántica para mantener consistencia con los reportes históricos, los eventos del outbox (que viajan con el precio cobrado) y la conciliación de pagos con MercadoPago.

## Consequences

- El `IPricingResolver` que se introducirá en el refactor de deep modules se invoca **solo** en el flujo Create del `Passenger`. No se llama en queries posteriores ni recalcula precios al leer.
- El campo `Passenger.Price` es autoritativo después de la creación. No es un cache ni una columna computed.
- Si en el futuro el negocio necesita "ajustar precios masivamente" (descuentos retroactivos, corrección de errores), eso requiere un caso de uso explícito que escriba el campo — no surge automáticamente de cambiar `TripPrice`.

## Considered alternatives

- **Derivado puro de `TripPrice`**: rechazado porque rompe reportes históricos y obliga a recalcular cada vez que se lee un pasajero. El cambio retroactivo de precios es indeseable para conciliación financiera.
- **Snapshot con override admin**: deferido. Hoy nadie pide modificar precios manualmente después de la compra. Si aparece el requirement se revisita este ADR.
