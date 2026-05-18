---
status: accepted
---

# La unicidad de `Service` no incluye `VehicleId`

Cada slot semanal de un `Service` está identificado únicamente por `(TenantId, TripId, DayOfWeek, DepartureHour)` filtrado por `Status = Active`. Esto significa que **no pueden existir dos `Service` activos para el mismo tramo, día y hora — ni siquiera con vehículos distintos**. Si la operación necesita aumentar capacidad para un slot, hoy se cambia el `Vehicle` del Service existente; no se "parte" el slot en dos buses paralelos. Aceptamos la restricción porque al momento del refactor los usuarios no tienen el caso de dos vehículos simultáneos sobre el mismo slot, y simplificar el modelo facilita la unicidad cross-batch/manual de `Reserve` (que tampoco incluye `VehicleId`).

## Consequences

- El batch `GenerateFutureReservesAsync` produce **a lo sumo una `Reserve` por `(TripId, ReserveDate, DepartureHour)`** porque sólo puede haber un `Service` activo aportando ese slot. No hay riesgo de colisión con el índice único de `Reserve`.
- Aumentar la capacidad de un slot recurrente = editar `Service.VehicleId` y apuntar a una unidad más grande. Es la única vía.
- La pantalla "Crear Nuevo Viaje" (Reserve manual) tampoco distingue por vehículo en la unicidad; un Reserve manual ocupa el slot `(Trip, Fecha, Hora)` independientemente del bus asignado.

## Trigger to revisit

Este ADR debe re-evaluarse cuando aparezca **cualquiera** de estos requirements:

1. Operación necesita correr dos buses en paralelo en el mismo slot (ej: alta demanda en feriados largos resuelta con dos unidades).
2. Reportes financieros que requieran trazabilidad de "qué vehículo cubrió qué slot" sin perder la capacidad de comparar vs el plan.
3. Cualquier feature de "asignación dinámica de vehículos" que necesite que un slot pueda quedar sin vehículo o cambiar de vehículo después de generadas las Reserves.

Si llega alguno, hay que incluir `VehicleId` en el índice único de `Service` y propagar el cambio al batch y al índice único de `Reserve`. Ese trabajo no es trivial: migration de schema, refactor de `GenerateFutureReservesAsync` (la idempotency key incluiría vehículo), y revisión de cualquier reporte que asume "un slot = una Reserve".

## Considered alternatives

- **Incluir `VehicleId` en la unicidad de Service**: rechazado. Hoy es overengineering — habilita un caso de uso que no existe (dos buses simultáneos), a costa de complicar la unicidad de `Reserve` (que tendría que distinguir también por vehículo o aceptar duplicados controlados por Service).
- **No imponer unicidad de Service**: rechazado. Permite cargar dos veces el mismo slot por error, y el batch generaría duplicados que rompen el índice único de `Reserve`.
