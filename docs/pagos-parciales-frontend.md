# Pagos Parciales y Saldado de Deuda — Guía Frontend

## Resumen de cambios

Se implementaron 3 cambios principales en el backend:

1. **Pagos parciales** en la creación de pasajeros (`passenger-reserves-create`)
2. **Pagos parciales** al agregar pagos posteriores (`reserve-payments-create`)
3. **Nuevo endpoint** para saldar deuda de múltiples reservas (`customer-debt-settle`)

---

## 1. Cambios en el status de pasajeros

### Antes
Al crear pasajeros, siempre quedaban en `Confirmed (2)`.

### Ahora

| Escenario | Status del pasajero |
|---|---|
| Admin crea pasajeros **sin pago** (payments vacío) | `PendingPayment (1)` |
| Admin crea pasajeros con **pago parcial** | `PendingPayment (1)` |
| Admin crea pasajeros con **pago completo** | `Confirmed (2)` |
| Se agrega un pago posterior que **completa** la deuda | `PendingPayment (1)` → `Confirmed (2)` |
| Se agrega un pago posterior **parcial** | Se mantiene `PendingPayment (1)` |

### Impacto en UI
- Los pasajeros con status `PendingPayment (1)` deben mostrarse visualmente diferente (ej: badge "Pendiente de pago", color amarillo/naranja).
- Solo los `Confirmed (2)` tienen su lugar asegurado.
- Ambos estados (`PendingPayment` y `Confirmed`) ocupan cupo en el vehículo.

### Enum completo de referencia

```
PendingPayment = 1
Confirmed      = 2
Cancelled      = 3
Traveled       = 4
NoShow         = 5
Refunded       = 6
```

---

## 2. Cambios en `POST /api/passenger-reserves-create`

### Request (sin cambios en estructura)
```json
{
  "payments": [
    { "transactionAmount": 50, "paymentMethod": 1 }
  ],
  "items": [
    {
      "reserveId": 1,
      "reserveTypeId": 1,
      "customerId": 5,
      "isPayment": true,
      "pickupLocationId": 10,
      "dropoffLocationId": 20,
      "hasTraveled": false,
      "price": 100
    }
  ]
}
```

### Cambios de comportamiento

| Campo `payments` | Resultado |
|---|---|
| `[]` (vacío) | Se crea el pasajero en `PendingPayment`. Se registra el cargo en `CurrentBalance`. No se crea `ReservePayment`. |
| `sum(payments) < price` | Pago parcial aceptado. Pasajero queda en `PendingPayment`. Se descuenta lo pagado del `CurrentBalance`. |
| `sum(payments) == price` | Pago completo. Pasajero queda en `Confirmed`. `CurrentBalance` queda en 0. |
| `sum(payments) > price` | **Error** `Reserve.OverPaymentNotAllowed` |

### Nuevos errores posibles

| Code | Descripción |
|---|---|
| `Reserve.OverPaymentNotAllowed` | El monto pagado supera el precio esperado |

> **Nota**: El error anterior `Reserve.InvalidPaymentAmount` ya no se retorna en este endpoint.

---

## 3. Cambios en `POST /api/reserve-payments-create/{reserveId}/{customerId}`

### Request (sin cambios en estructura)
```json
[
  { "transactionAmount": 40, "paymentMethod": 1 }
]
```

### Cambios de comportamiento

Ahora el backend **acumula pagos anteriores** para determinar cuánto falta por pagar:

```
deudaRestante = sumaPreciosPasajeros - sumaPagosPadresPrevios(status=Paid)
```

| Escenario | Resultado |
|---|---|
| No hay deuda pendiente (ya se pagó todo) | **Error** `Reserve.AlreadyFullyPaid` |
| `sum(payments) > deudaRestante` | **Error** `Reserve.OverPaymentNotAllowed` |
| `sum(payments) < deudaRestante` | Pago parcial OK. Pasajeros se mantienen en `PendingPayment`. |
| `sum(payments) == deudaRestante` | Deuda saldada. Pasajeros cambian a `Confirmed`. |

### Nuevos errores posibles

| Code | Descripción |
|---|---|
| `Reserve.OverPaymentNotAllowed` | El monto pagado supera la deuda pendiente |
| `Reserve.AlreadyFullyPaid` | La reserva ya está completamente pagada |

### Ejemplo de flujo multi-pago

```
1. Crear pasajero con precio $100, sin pago        → PendingPayment, CurrentBalance = 100
2. POST reserve-payments-create con $60             → PendingPayment, CurrentBalance = 40
3. POST reserve-payments-create con $40             → Confirmed, CurrentBalance = 0
4. POST reserve-payments-create con cualquier monto → Error: AlreadyFullyPaid
```

---

## 4. Nuevo endpoint: `POST /api/customer-debt-settle`

Permite al admin seleccionar N reservas de un cliente y aplicar un pago que las salda total o parcialmente.

### Request
```json
{
  "customerId": 5,
  "reserveIds": [10, 15, 22],
  "payments": [
    { "transactionAmount": 200, "paymentMethod": 1 },
    { "transactionAmount": 100, "paymentMethod": 4 }
  ]
}
```

| Campo | Tipo | Requerido | Descripción |
|---|---|---|---|
| `customerId` | `int` | Si | ID del cliente |
| `reserveIds` | `int[]` | Si | IDs de las reservas a saldar |
| `payments` | `CreatePaymentRequestDto[]` | Si | Pagos a aplicar |
| `payments[].transactionAmount` | `decimal` | Si | Monto > 0 |
| `payments[].paymentMethod` | `int` | Si | 1=Cash, 2=Online, 3=CreditCard, 4=Transfer |

### Response
```json
{
  "isSuccess": true,
  "value": true
}
```

### Lógica de distribución del pago

El pago se distribuye **secuencialmente** por las reservas en el orden en que se envían en `reserveIds`:

```
Ejemplo: reserveIds = [10, 15], deudas = [$100, $80], pago total = $150

→ Reserva #10: aplica $100 → deuda saldada → pasajeros cambian a Confirmed
→ Reserva #15: aplica $50  → deuda parcial  → pasajeros siguen en PendingPayment
```

### Errores posibles

| Code | Descripción |
|---|---|
| `Customer.NotFound` | El cliente no existe |
| `Reserve.NotFound` | Ninguna de las reservas existe |
| `Reserve.NoDebtToSettle` | Todas las reservas seleccionadas ya están pagadas |
| `Reserve.OverPaymentNotAllowed` | El monto total supera la deuda total de las reservas |
| `Payments.InvalidAmount` | Algún pago tiene monto <= 0 |
| `Payments.DuplicatedMethod` | Métodos de pago repetidos |
| `CashBox.NotFound` | No hay caja abierta |

### Autorización
- Requiere rol **Admin**

---

## 5. Sugerencias de implementación para UI

### Vista de pasajeros de una reserva
- Mostrar badge de estado diferenciado para `PendingPayment` vs `Confirmed`
- Mostrar monto pagado vs monto total (se puede calcular desde los `ReservePayments` existentes o desde `CurrentBalance` del cliente)
- Botón "Agregar pago" habilitado solo si hay pasajeros en `PendingPayment`

### Flujo de creación de pasajeros
- Permitir enviar `payments: []` para crear sin pago
- Permitir que el monto de los pagos sea menor al precio total
- Validar en frontend que `sum(payments) <= precio total` antes de enviar

### Vista de saldado de deuda (nueva)
- Pantalla donde el admin selecciona un cliente
- Listar reservas del cliente que tengan deuda pendiente (pasajeros en `PendingPayment`)
- Mostrar deuda por reserva y deuda total
- Permitir seleccionar N reservas y especificar montos de pago
- Validar que `sum(payments) <= deuda total seleccionada`
- Llamar a `POST /api/customer-debt-settle`
- Refrescar la vista para mostrar qué reservas quedaron saldadas (`Confirmed`) y cuáles siguen pendientes

---

## 6. Métodos de pago (referencia)

```
Cash       = 1
Online     = 2
CreditCard = 3
Transfer   = 4
```

No se permite repetir el mismo método en un mismo request de pagos. Si se necesitan 2 montos distintos, deben ser métodos distintos (split de medios).
