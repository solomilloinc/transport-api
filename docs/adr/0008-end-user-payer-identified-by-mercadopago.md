---
status: proposed
---

# El pagador del alta de usuario final se identifica por el pago de MercadoPago (puede ser un tercero)

En el alta de usuario final (flujo external, Card/Wallet Brick de MercadoPago) había un bug: cuando el que paga no existía como `Customer`, no se daba de alta, y la `Reserve`/`Passenger`/`ReservePayment` quedaban con `CustomerId = null` — sin forma de reintegrar al cancelar. El primer arreglo identificaba al pagador como un pasajero (`IsPayment`, o el primero del array), pero eso es frágil: **el pagador es quien identifica el pago en MercadoPago, y no necesariamente viaja** (ej. un padre que paga por sus hijos). Este ADR fija cómo se identifica y materializa al pagador, sin introducir el agregado `Booking` (ADR 0002).

## Decisiones

- **Identidad del pagador = el `Payer` del pago de MercadoPago** (número de identificación + email), no un flag/posición de pasajero. `PassengerBookingExternalDto.IsPayment` deja de ser la fuente de verdad del pagador.
- **El pagador se materializa como `Customer`; los pasajeros se relacionan a él vía `Passenger.ReservePaymentId` → `ReservePayment.CustomerId`**, no por `Passenger.CustomerId`. Así `Passenger.CustomerId` mantiene su semántica "quién ES el pasajero" (null para invitados) y se agrega un vínculo asiento→pago→pagador sin agregado `Booking` (consistente con ADR 0002). Se permite que el pagador sea un **tercero que no viaja**.
- **Resolver-o-crear el `Customer` del pagador por documento**, con prioridad: (1) `Customer` existente con ese `DocumentNumber`; (2) un `Passenger` del booking con ese mismo documento (el que paga también viaja → se reusa su perfil); (3) tercero real → se crea con los datos del `Payer` de MP.
- **El perfil del tercero (`FirstName`/`LastName`/`Phone1`, requeridos) se toma del objeto `Payer` de MercadoPago** (`CreatePaymentAsync` en Card; `GetPaymentAsync` en el webhook para Wallet). Si MP no devuelve nombre, se setea por defecto desde el cardholder; el teléfono cae a vacío si no viene. **Supuesto a verificar:** que MP devuelva consistentemente los datos del payer (ver Trigger to revisit).
- **Card vs Wallet — cuándo se resuelve:**
  - **Card:** el `Payer` está disponible en el alta (`request.Payment` + respuesta de `CreatePaymentAsync`) → se resuelve ahí.
  - **Wallet:** el `Payer` recién se conoce en el **webhook** (`UpdateReservePaymentsByExternalId`). En el alta, el `ReservePayment` pendiente se crea con `CustomerId = null`; el webhook resuelve/crea el `Customer` y setea `ReservePayment.CustomerId`. Los `Passenger` ya cargan `ReservePaymentId` desde el alta, así que la cadena del refund se completa al confirmarse el pago.
- **El refund del cancel (ADR 0007) no cambia:** sigue acreditando saldo a favor a `ReservePayment.CustomerId`. La cadena `Passenger.ReservePaymentId → ReservePayment.CustomerId` hace alcanzable al pagador incluso para acompañantes con `CustomerId = null`.

## Consequences

- `Passenger` gana `ReservePaymentId` (FK nullable a `ReservePayment` + índice + migración). Liga asiento → pago → pagador.
- Los acompañantes (no pagadores) quedan con `Passenger.CustomerId = null`: no se les estampa el `CustomerId` del pagador (esa opción se rechazó por romper la semántica "CustomerId = identidad propia").
- En Wallet, el `Customer` del pagador y el `CustomerId` del pago se completan en el **webhook**, no en el alta. Un checkout de Wallet abandonado no crea `Customer` ni setea `CustomerId` (el pago queda pendiente con `CustomerId = null`).
- El pagador puede quedar como un `Customer` "tercero" con perfil parcial (nombre del cardholder, sin teléfono). Aceptable para el refund (saldo a favor); su consumo lo gestionan los flujos de cobro existentes.
- Datos históricos: reservas external previas a este cambio tienen `ReservePaymentId = null` en los acompañantes → su cancel no reintegra. Backfill opcional imputando el pago padre por `ReserveId`.

## Considered alternatives

- **Identificar al pagador por `IsPayment`/primer pasajero**: rechazado. El pagador no necesariamente es pasajero ni el primero del array; la identidad real la da MercadoPago.
- **Estampar el `CustomerId` del pagador en todos los `Passenger`** (como el alta admin, ADR 0007): rechazado. Rompe `Passenger.CustomerId` = "quién ES el pasajero" y mezcla identidad con "quién pagó".
- **Extender el payload del pago con `PayerFirstName`/`LastName`/`Phone`**: considerado, diferido. El Wallet Brick no renderiza nativamente esos campos, así que no sería uniforme entre Card y Wallet. Queda como alternativa si el `Payer` de MP resulta insuficiente.
- **Introducir el agregado `Booking`**: rechazado (ADR 0002). `Passenger.ReservePaymentId` es el vínculo liviano que alcanza para este caso.

## Trigger to revisit

- **Verificar que MercadoPago devuelva consistentemente los datos del `Payer`** (identificación/email/nombre) tanto en Card como en Wallet. Si no es confiable, corresponde extender el payload del pago (alternativa diferida) o capturar los datos del pagador explícitamente en el frontend.
- Si se necesita un **reembolso real a MercadoPago** (devolver plata, no solo saldo a favor), se cruza con el Trigger de ADR 0007 y la atribución por pasajero/pago se vuelve más exigente.
