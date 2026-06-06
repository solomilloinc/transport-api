# Cancelar Pasajero — endpoint nuevo

**Audiencia:** equipo / agente frontend.
**Estado:** backend implementado (no deployado).
**Wire format:** **camelCase** (input + output), igual que el resto de la API.
**Endpoint nuevo:** `POST /passenger-cancel/{passengerId}`
**Auth:** rol **Admin**.
**Decisión de diseño completa:** [docs/adr/0007-cancel-passenger-reverts-debt-as-credit.md](../adr/0007-cancel-passenger-reverts-debt-as-credit.md).

> **Alcance:** por ahora **solo cancelación**. Mover/reubicar pasajeros a otra reserva **no** está
> en alcance hasta nuevo aviso. Este doc cubre únicamente cancelar.

---

## 1. Qué hace

En la grilla de pasajeros de una reserva, una acción **Cancelar** por fila (solo Admin). Da de baja
al pasajero y le devuelve la plata como **saldo a favor** en su cuenta corriente.

Qué hace el backend (para que la UI lo refleje):

- Marca al pasajero como **Cancelled**.
- Revierte su deuda: genera un movimiento de cuenta corriente que **baja el saldo del cliente** por
  el precio del pasajero.
  - Si **no había pagado** → su deuda queda en cero.
  - Si **ya había pagado** → el cliente queda con **saldo a favor** (`currentBalance` negativo) para
    futuros viajes. **No se devuelve efectivo y la caja no se toca** (la plata ya entró; regla
    "caja en cero").
- **IdaVuelta:** si el pasajero es parte de un viaje ida y vuelta, **se cancelan las dos piernas
  juntas** (ida + vuelta de esa misma persona) y se revierte el precio del paquete una sola vez. La
  UI debería avisar al operador ("Este pasajero tiene ida y vuelta; se cancelarán ambos tramos").

### Multi-pasajero (importante)

Cancelar opera **solo sobre el pasajero seleccionado**. Cada `Passenger` tiene su propio precio.
Cancelar al pasajero X **no** toca a los demás pasajeros de la misma compra/reserva. Si una familia
de 4 viaja y uno se baja, cancelás solo a ese (su precio). La única excepción es el par IdaVuelta de
**esa misma persona** (sus dos piernas), nunca las de los demás.

### Lado usuario final (público)

Cancelar es **solo admin**. El flujo público (checkout MercadoPago) no lo expone: un cliente que
compró no puede auto-cancelarse. Sin cambios del lado usuario.

---

## 2. `POST /passenger-cancel/{passengerId}`

### Request

Sin body. El `passengerId` va en la URL.

```
POST /passenger-cancel/123
```

### Response

`200 OK` con `true` si salió bien. Errores como ProblemDetails (ver §3).

### Cuándo se puede cancelar (elegibilidad)

Solo si el pasajero está **activo** (`PendingPayment` o `Confirmed`) **y su reserva no partió
todavía**. Si no, devuelve error accionable (ver §3). Conviene **deshabilitar** la acción en la UI
para pasajeros ya cancelados / viajados / no-show / reembolsados, y para reservas ya partidas.

```ts
const canCancel = [1 /*PendingPayment*/, 2 /*Confirmed*/].includes(row.status)
               && !rowReserveHasDeparted;
```

---

## 3. Manejo de errores (ProblemDetails / RFC 7807)

```jsonc
{
  "title": "Passenger.NotActive",   // el "code" del error (úsalo para lógica)
  "detail": "El pasajero no está activo (estado: Traveled). Solo se pueden cancelar pasajeros pendientes de pago o confirmados.",
  "status": 400,
  "type": "https://tools.ietf.org/html/rfc7231#..."
}
```

`title` es el **código** estable del error (matchealo en código); `detail` es el mensaje en español
listo para mostrar.

| `title` (code)              | HTTP | Cuándo                                                          |
|----------------------------|------|----------------------------------------------------------------|
| `Passenger.NotFound`       | 404  | El `passengerId` no existe.                                     |
| `Passenger.NotActive`      | 400  | El pasajero ya está cancelado / viajó / no-show / reembolsado.  |
| `Passenger.ReserveDeparted`| 400  | La reserva del pasajero ya partió.                              |

---

## 4. Tipos TypeScript de referencia

```ts
// Llamada
await api.post(`/passenger-cancel/${passengerId}`);
```

---

## 5. Checklist frontend

- [ ] Grilla de pasajeros: agregar acción **Cancelar** por fila (solo Admin).
- [ ] Habilitar solo si el pasajero está activo (`status` 1 o 2) y su reserva no partió; sino,
      deshabilitar.
- [ ] Confirmación antes de cancelar. Aviso especial para IdaVuelta ("se cancelan ambos tramos").
- [ ] Refrescar la grilla y el saldo del cliente al volver (el `currentBalance` puede quedar a
      favor / negativo).
- [ ] Mapear los `title` de error de la tabla §3 a mensajes/UX (usar `detail` para el texto).
```
