# Cambios en API de reservas: pricing por fechas + nuevo shape del request

**Audiencia:** equipo / agente frontend.
**Estado:** implementado en backend, no deployado.
**Breaking change:** SÍ — incluye:
1. Body del POST de creación de pasajeros cambia (nuevo shape)
2. **Wire format ahora es camelCase en TODA la API** (input + output)

---

## 0. Convención de wire format

A partir de este cambio, **toda la API usa camelCase** en JSON, tanto en requests como en responses. Esto unifica la convención y permite usar interfaces TypeScript sin mappers.

- ✅ Requests: mandar todas las propiedades en **camelCase** (`reserveTypeId`, `passengers`, `outbound`)
- ✅ Responses: el server devuelve siempre **camelCase** (`tripId`, `dropoffOptionsIda`, `passengerId`)
- ✅ Compatibilidad de entrada: el parseo del server es **case-insensitive**, así que PascalCase legacy también funcionará durante la transición — pero el equipo debe migrar a camelCase

**Cambio backend que lo habilita:** `JsonObjectSerializer(JsonSerializerDefaults.Web)` en [Program.cs](transport.api/Program.cs:30).

---

## 1. Cambio de regla de negocio

### Antes
El precio de un booking IdaVuelta usaba SIEMPRE el `TripPrice` de tipo `IdaVuelta` (precio con descuento), independientemente de las fechas de las dos reservas.

### Ahora
El descuento IdaVuelta solo se aplica si las dos reservas (outbound + return) son del **mismo día calendario**. Si las fechas difieren, cada pierna se cobra al precio `Ida` (sin descuento), o sea ≈ 2× el precio Ida.

La regla es **configurable por tenant** via `TenantConfig.RoundTripRequiresSameDay` (default `true`). Si un tenant la desactiva, el comportamiento legacy vuelve (siempre IdaVuelta sin importar fechas).

### Cómo el frontend conoce el flag del tenant

El config que ya devuelve `GET /tenant/resolve?host=...` (primera llamada del frontend al cargar) y `GET /tenant/config` ahora incluye una sección `businessRules`:

```json
{
  "code": "zerostour",
  "publicKey": "...",
  "config": {
    "identity": { "...": "..." },
    "contact":  { "...": "..." },
    "legal":    { "...": "..." },
    "businessRules": {
      "roundTripRequiresSameDay": true
    }
  }
}
```

El frontend lee `config.businessRules.roundTripRequiresSameDay` una sola vez al iniciar y lo guarda en su store de tenant.

**Default defensivo:** si el campo no viene (config viejo cacheado), asumir `true` — coincide con el default del backend.

### Implicación para el pricing en el frontend

`GET /trip/{id}` → `TripReportResponseDto` devuelve ambas tablas de precios:
- `DropoffOptionsIda` — precio Ida por dropoff
- `DropoffOptionsIdaVuelta` — precio IdaVuelta por dropoff

Cuando el usuario seleccionó outbound + return, el frontend decide qué tabla usar combinando el flag del tenant con las fechas:

```ts
const sameDay = outboundReserve.reserveDate.slice(0, 10) === returnReserve.reserveDate.slice(0, 10);

// Si el tenant exige same-day y las fechas difieren, el descuento NO aplica
const useIdaVueltaTariff = !tenant.businessRules.roundTripRequiresSameDay || sameDay;

const dropoffOptions = useIdaVueltaTariff
  ? trip.dropoffOptionsIdaVuelta
  : trip.dropoffOptionsIda;

const pricePerLeg = dropoffOptions.find(o => o.cityId === selectedDropoffCityId)?.price;
```

Reglas concretas:
- Tenant con `roundTripRequiresSameDay = false` → siempre usa precios `DropoffOptionsIdaVuelta` (independiente de fechas)
- Tenant con `roundTripRequiresSameDay = true` Y mismo día → `DropoffOptionsIdaVuelta`
- Tenant con `roundTripRequiresSameDay = true` Y días distintos → `DropoffOptionsIda` × 2 piernas

**El server valida**. Si mandás IdaVuelta-tariff con fechas distintas y el tenant tiene la regla activa, te rechaza con `Reserve.PriceNotAvailable`.

---

## 2. Nuevo shape del request de creación de pasajeros

### Endpoint: `POST /passenger-reserves-create` (admin)

#### Antes (obsoleto — mezcla de PascalCase/camelCase y shape viejo)
```json
{
  "payments": [{ "transactionAmount": 100, "paymentMethod": 1 }],
  "items": [
    { "reserveId": 1, "reserveTypeId": 2, "customerId": 1, "isPayment": true,
      "pickupLocationId": 1, "dropoffLocationId": 2, "hasTraveled": false,
      "price": 80, "reserveRelatedId": 2 },
    { "reserveId": 2, "reserveTypeId": 2, "customerId": 1, "isPayment": true,
      "pickupLocationId": 2, "dropoffLocationId": 1, "hasTraveled": false,
      "price": 80, "reserveRelatedId": 1 }
  ]
}
```

#### Ahora (camelCase, nuevo shape)
```json
{
  "reserveTypeId": 2,
  "outboundReserveId": 1,
  "returnReserveId": 2,
  "payments": [{ "transactionAmount": 160, "paymentMethod": 1 }],
  "passengers": [
    {
      "customerId": 1,
      "isPayment": true,
      "hasTraveled": false,
      "outbound": {
        "pickupLocationId": 1,
        "dropoffLocationId": 2,
        "price": 80
      },
      "return": {
        "pickupLocationId": 2,
        "dropoffLocationId": 1,
        "price": 80
      }
    }
  ]
}
```

Para una reserva **Ida** (sin vuelta):
```json
{
  "reserveTypeId": 1,
  "outboundReserveId": 1,
  "returnReserveId": null,
  "payments": [{ "transactionAmount": 100, "paymentMethod": 1 }],
  "passengers": [
    {
      "customerId": 1,
      "isPayment": true,
      "hasTraveled": false,
      "outbound": { "pickupLocationId": 1, "dropoffLocationId": 2, "price": 100 },
      "return": null
    }
  ]
}
```

### Endpoint público (pasajero comprando online): `POST /passenger-reserves-create-with-lock`

Para usuarios públicos el flujo es: primero `POST /reserve-slots-lock` para reservar cupos (devuelve `lockToken`), después este endpoint para confirmar la compra. El shape del body:

```json
{
  "lockToken": "<guid>",
  "reserveTypeId": 2,
  "outboundReserveId": 1,
  "returnReserveId": 2,
  "payment": { "transactionAmount": 160, "paymentMethodId": "...", "token": "...", "...": "..." },
  "passengers": [
    {
      "customerId": null,
      "isPayment": true,
      "hasTraveled": false,
      "firstName": "Juan",
      "lastName": "Pérez",
      "email": "juan@example.com",
      "phone1": "+541112345678",
      "documentNumber": "30123456",
      "outbound": { "pickupLocationId": 1, "dropoffLocationId": 2, "price": 80 },
      "return":   { "pickupLocationId": 2, "dropoffLocationId": 1, "price": 80 }
    }
  ]
}
```

`payment` puede ser `null` cuando el pago es asincrónico (devuelve preferencia MercadoPago); cuando viene completo, el server cobra inmediatamente y confirma a los pasajeros.

---

## 3. Reglas de validación que el server enforza

| # | Regla | Error |
|---|-------|-------|
| 1 | `ReserveTypeId = 2` (IdaVuelta) ⇒ `ReturnReserveId` requerido | Validation |
| 2 | `ReserveTypeId = 1` (Ida) ⇒ `ReturnReserveId` debe ser `null` | Validation |
| 3 | `ReserveTypeId = 2` ⇒ cada pasajero debe tener `Return != null` | Validation |
| 4 | `ReserveTypeId = 1` ⇒ cada pasajero debe tener `Return = null` | Validation |
| 5 | `OutboundReserveId != ReturnReserveId` | Validation |
| 6 | Al menos un pasajero | Validation |
| 7 | El `Price` de cada pierna debe coincidir con la tarifa que el server calcula | `Reserve.PriceNotAvailable` |
| 8 | Idem para la pierna Return | `Reserve.PriceNotAvailable` |

La regla 7-8 es donde se aplica el toggle del tenant + check de fechas: si el server resolvió `effectiveType = Ida` (por fechas distintas y regla activa), va a esperar `Price = precioIda` en ambas piernas.

---

## 4. Total esperado en `payments`

El backend calcula `totalExpectedAmount = Sum(outbound.price + return?.price ?? 0)` sobre todos los pasajeros.

| Escenario | Cálculo |
|-----------|---------|
| Ida, 1 pax, $100 | `100` |
| Ida, 3 pax, $100 c/u | `300` |
| IdaVuelta mismo día, 1 pax, $80 c/pierna | `160` |
| IdaVuelta días distintos, 1 pax, $100 c/pierna (Ida) | `200` |

El frontend debe mandar `payments[].transactionAmount` (o `payment.transactionAmount` en el endpoint público) sumando exactamente lo que el server espera; sobrepagos se rechazan con `Reserve.OverPaymentNotAllowed`.

---

## 5. Campos eliminados / renombrados

| Viejo | Nuevo |
|-------|-------|
| `items[].reserveId` | `outboundReserveId` / `returnReserveId` a nivel wrapper |
| `items[].reserveTypeId` | `reserveTypeId` a nivel wrapper |
| `items[].reserveRelatedId` | (eliminado — se deriva de `outboundReserveId`/`returnReserveId`) |
| `items[].pickupLocationId/dropoffLocationId/price` | `passengers[].outbound.{pickupLocationId,dropoffLocationId,price}` y `passengers[].return.…` |
| `items[].customerId/isPayment/hasTraveled` | `passengers[].customerId/isPayment/hasTraveled` |
| `items[].firstName/lastName/...` (público) | `passengers[].firstName/lastName/...` |

---

## 6. Lógica que el frontend tiene que implementar

1. Al cargar la app: leer `config.businessRules.roundTripRequiresSameDay` del response de `/tenant/resolve` y guardarlo en el store (default `true` si no viene).

2. Después de que el usuario seleccione outbound + return (si aplica IdaVuelta):
   - Si `roundTripRequiresSameDay === false` → tomar precios de `dropoffOptionsIdaVuelta` directamente
   - Si `roundTripRequiresSameDay === true`:
     - Comparar `outboundReserve.reserveDate.toDate()` vs `returnReserve.reserveDate.toDate()` por **fecha calendario** (zona horaria Argentina; ignorar hora)
     - Si igual → `dropoffOptionsIdaVuelta`
     - Si distinto → `dropoffOptionsIda` para ambas piernas

3. Armar `passengers[]` con un objeto por pasajero, cada uno con `outbound` y `return` (este último solo cuando IdaVuelta).

4. Mandar `reserveTypeId = 2` (IdaVuelta) **siempre que sea round-trip**, incluso si por fechas el server va a cobrar Ida. El `reserveTypeId` es la **intención de compra**, no el precio efectivo. Esto preserva el dato "esta venta era round-trip" en reportes.

5. Calcular `payments[].transactionAmount` (o `payment.transactionAmount` en el público) como suma de todos los precios mandados.

---

## 7. Tipos TypeScript de referencia

```ts
type ReserveTypeId = 1 | 2; // 1=Ida, 2=IdaVuelta

interface LegInfo {
  pickupLocationId: number | null;
  dropoffLocationId: number | null;
  price: number;
}

interface PassengerBooking {
  customerId: number;
  isPayment: boolean;
  hasTraveled: boolean;
  outbound: LegInfo;
  return: LegInfo | null; // null si Ida
}

interface PassengerReserveCreateRequestWrapper {
  reserveTypeId: ReserveTypeId;
  outboundReserveId: number;
  returnReserveId: number | null;
  payments: Array<{ transactionAmount: number; paymentMethod: number }>;
  passengers: PassengerBooking[];
}

// Público (vía lock) — agrega LockToken y datos completos por pasajero
interface PassengerBookingExternal {
  customerId: number | null;
  isPayment: boolean;
  hasTraveled: boolean;
  firstName: string;
  lastName: string;
  email: string | null;
  phone1: string;
  documentNumber: string;
  outbound: LegInfo;
  return: LegInfo | null;
}

interface CreateReserveWithLockRequest {
  lockToken: string;
  reserveTypeId: ReserveTypeId;
  outboundReserveId: number;
  returnReserveId: number | null;
  payment: ExternalPayment | null;
  passengers: PassengerBookingExternal[];
}
```

---

## 8. Cambios en `TripReportResponseDto`

Sin cambios de shape de payload. Solo:
- Las propiedades ahora se serializan en camelCase: `tripId`, `dropoffOptionsIda`, `dropoffOptionsIdaVuelta`, etc.
- Documentación XML agregada a `dropoffOptionsIdaVuelta` explicando que esos precios solo son válidos si las dos reservas son del mismo día.

---

## 9. Resumen para el agente frontend

- **Migrar todo el wire a camelCase** (input + output). Las interfaces TS ya están listas para usarse sin mappers.
- Cambiar el shape del body en los endpoints de creación (admin + público con lock)
- Leer `config.businessRules.roundTripRequiresSameDay` al iniciar y guardarlo en el store
- Implementar la comparación de fechas client-side para decidir qué precios mostrar/mandar
- `reserveTypeId` siempre representa la INTENCIÓN (Ida vs IdaVuelta), no la tarifa
- El server es la autoridad final: si el cálculo de precio no coincide, el booking falla con `Reserve.PriceNotAvailable`
- Total = suma de todas las piernas de todos los pasajeros (cambió respecto al cálculo anterior)
