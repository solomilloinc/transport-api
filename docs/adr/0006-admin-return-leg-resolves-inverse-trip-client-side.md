# El alta admin de IdaVuelta resuelve el Trip inverso en el frontend

En el alta admin de una reserva IdaVuelta, la lista de "viajes de vuelta" debe mostrar **solo la ruta inversa** (el `Trip` con Origin/Destination intercambiados respecto de la ida). Decidimos que el **frontend** resuelva cuál es ese `Trip inverso` (contra el catálogo de Trips) y se lo pase al reporte por día (`reserve-report/{date}`) como un filtro **estricto** de `TripId`, en vez de resolverlo server-side. El backend solo aplica el filtro `TripId` que recibe; no infiere la inversa.

## Considered Options

- **FE resuelve la inversa + `TripId` estricto en el reporte por día (elegido)**: un único cambio mínimo de backend (el filtro `TripId` opcional en `reserve-report/{date}`) sirve para los dos requerimientos — el Select por Trip de la página de Reservas y la pierna de vuelta del IdaVuelta. El frontend ya maneja el concepto de trip de vuelta para pricing.
- **Backend resuelve la inversa**: mandar el `TripId` de la **ida** + un flag "pierna de vuelta" y que el backend haga el swap Origin/Destination. Mantiene la regla del dominio server-side, pero el mismo parámetro pasa a significar dos cosas según el flag.
- **Reusar el endpoint agrupado público** (`public/reserve-summary`), que ya resuelve la inversa server-side ([ReserveReportBusiness.cs](../../transport.application/ReserveReportBusiness/ReserveReportBusiness.cs) overload 2): cero lógica nueva, pero arrastra el filtrado por capacidad (oculta reservas llenas) y un pricing que el alta admin calcula por su cuenta, además de estar bajo `[AllowAnonymous]`.

## Consequences

- **Quedan dos mecanismos para la misma regla de dominio** ("encontrar el Trip inverso"): el flujo público lo resuelve server-side en el reporte agrupado; el alta admin lo resuelve en el frontend. Es la deuda deliberada que este ADR deja anotada para que nadie lo "arregle" asumiendo que uno de los dos sobra. Ver `CONTEXT.md → Trip inverso (ruta de vuelta)`.
- **Footgun cerrado por contrato de frontend**: como `reserve-report/{date}` trata `tripId` ausente/0 como "todas las reservas del día" (lo que necesita la página de Reservas), si el alta admin llamara a la lista de vuelta **sin** un `TripId` inverso resuelto, reaparecería el bug original (ambos sentidos). Por eso el frontend tiene la **regla dura**: la lista de la pierna de vuelta **siempre** se llama con un `TripId` inverso resuelto; si no existe `Trip` inverso configurado, muestra un estado vacío ("no hay ruta de vuelta") y **no** llama al endpoint.
- Si una ciudad-par no tiene su `Trip` inverso configurado, la pierna de vuelta no es reservable desde el admin hasta que se cree ese `Trip` — comportamiento explícito, no un error silencioso.
- Si en el futuro se quiere una única fuente de verdad para la inversa, este ADR avisa que la duplicación fue una decisión, no un olvido; el camino sería mover la resolución al backend (opción 2 o 3).
