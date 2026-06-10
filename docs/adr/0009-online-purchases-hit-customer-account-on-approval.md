---
status: accepted
---

# La compra online asienta en cuenta corriente al APROBARSE el pago (Charge + Pago juntos, neto 0)

El flujo de usuario final (external, MercadoPago) creaba `ReservePayment` pero **nunca escribía la cuenta corriente** (`CustomerAccountTransaction` / `CurrentBalance`), a diferencia del flujo admin que asienta `Charge` al alta y `Payment` al cobrar. Eso era crítico para el caso real: un admin cancela un pasajero de una compra online pagada → el `Refund` (ADR 0007) debe dejar el pago como **saldo a favor** en la cuenta del pagador — pero sin `Charge`/`Payment` previos el crédito quedaba huérfano, sin extracto que lo respalde (y el balance neto del cliente dependía de asientos que no existían).

## Decisiones

- **La compra online se asienta cuando el pago queda APROBADO, no al alta.** Se escriben **juntos** un `Charge` (+total) y un `Payment` (−total) a nombre del pagador (`ReservePayment.CustomerId`, ADR 0008), con `CurrentBalance` neto 0: compró y pagó. El `Payment` referencia el `ReservePayment` (`ReservePaymentId`), el `Charge` la reserva principal (`RelatedReserveId`).
- **Dónde corre cada caso:**
  - **Card aprobado sincrónico** → en el mismo alta (`ReserveBusiness.CreatePayment`), tras la respuesta del gateway.
  - **Wallet, o Card `pending`/`in_process`** → en el **webhook** (`ReservePaymentBusiness.RegisterApprovedOnlinePurchaseAsync`, invocado por `ProcessPaymentFromWebhook` y `UpdateReservePaymentsByExternalId`) cuando el pago transiciona `Pending → Paid`.
  - **Rechazado o checkout abandonado** → no se asienta nada: la compra no existió.
- **Idempotencia:** el guard de entrada del webhook (pagos ya no-`Pending` no se reprocesan) garantiza un único asiento. El caso card-aprobado-sincrónico queda `Paid` de entrada, así que el webhook posterior lo ignora.
- **Si el pago no tiene `Customer` vinculado** (pagador no resuelto aún, ADR 0008 — ej. tercero que no viaja, pendiente de materializar) **no se asienta**: sin cliente no hay cuenta. Cuando se complete la resolución del pagador en el webhook (ADR 0008), este asiento la acompaña.

## Por qué al aprobar y no al alta

El flujo admin asienta el `Charge` al alta porque ahí la deuda es real (pago en destino/parcial). En el online no: si el `Charge` se escribiera al alta, cada checkout de **Wallet abandonado** (el webhook nunca llega) dejaría **deuda fantasma** en la cuenta del cliente, y un card rechazado exigiría reversión inmediata. Asentando al aprobar, el ledger refleja solo compras concretadas; la "deuda online" no existe como concepto porque el flujo external exige pago.

## Consequences

- El extracto del cliente muestra su compra online completa (`Charge` + `Pago online`) y el escenario crítico cierra: cancelación admin → `Refund −Price` → `CurrentBalance` negativo = saldo a favor respaldado por el extracto.
- Los reportes de cuenta corriente (`GetCustomerTransactions`, totales de cargos/pagos por rango) ahora incluyen la actividad online.
- Una compra wallet pendiente no aparece en la cuenta hasta confirmarse; entre alta y webhook el extracto no la muestra (los `Passenger` quedan `PendingPayment`, visibles por los reportes de reserva, no por cuenta corriente).
- Compras online históricas (previas a este cambio) no tienen asientos; sus refunds quedarán como crédito sin Charge/Pago previo, igual que hasta ahora. Backfill posible desde `ReservePayment` pagos si hiciera falta.

## Considered alternatives

- **`Charge` al alta + `Payment` al aprobar (espejo exacto del admin):** rechazado — deuda fantasma en checkouts wallet abandonados y reversión obligada en rechazos.
- **No asentar nada y excluir las compras online de la cuenta corriente:** rechazado — es el estado actual que motiva este ADR; deja el `Refund` del cancel sin respaldo.
- **Asentar solo el `Payment` (sin `Charge`):** rechazado — dejaría el balance negativo permanente (como si todo pago online fuera saldo a favor) y el extracto sin la compra que lo originó.

## Trigger to revisit

- Si aparece **pago online parcial** (hoy el external valida monto exacto), el neto-0 deja de ser invariante y el asiento debe partirse como en el flujo admin.
- Si se implementa la **resolución del pagador en el webhook** (ADR 0008, wallet/tercero), revisar que ese paso preceda al asiento para no saltearlo por `CustomerId = null`.
