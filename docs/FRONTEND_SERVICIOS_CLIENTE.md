# Asociar Clientes con Servicios - Guía Frontend

## Resumen

Se implementó la funcionalidad para asociar clientes con servicios. Esto permite:
1. Obtener una lista de servicios activos para mostrar en dropdowns
2. Asociar servicios al crear o actualizar un cliente

---

## 1. Obtener Lista de Servicios Activos

### Endpoint
```
GET /api/services-list
```

### Headers
```
Authorization: Bearer {token}
```

### Response (200 OK)
```json
[
  { "serviceId": 1, "name": "Servicio Buenos Aires - Rosario" },
  { "serviceId": 2, "name": "Servicio Córdoba - Mendoza" },
  { "serviceId": 3, "name": "Servicio Express Nocturno" }
]
```

### Uso
Este endpoint devuelve solo servicios con `Status = Active`. Ideal para popular un dropdown o multiselect de servicios.

---

## 2. Crear Cliente con Servicios

### Endpoint
```
POST /api/customer-create
```

### Headers
```
Authorization: Bearer {token}
Content-Type: application/json
```

### Request Body
```json
{
  "firstName": "Juan",
  "lastName": "Pérez",
  "email": "juan.perez@email.com",
  "documentNumber": "12345678",
  "phone1": "1122334455",
  "phone2": "1199887766",
  "serviceIds": [1, 2, 3]
}
```

### Campos ServiceIds

| Valor | Comportamiento |
|-------|----------------|
| `null` o no enviado | Cliente sin servicios asociados |
| `[]` (array vacío) | Cliente sin servicios asociados |
| `[1, 2, 3]` | Cliente con servicios 1, 2 y 3 asociados |

### Response (200 OK)
```json
5  // customerId creado
```

### Errores posibles
```json
// Si un serviceId no existe o no está activo:
{
  "code": "Service.ServiceNotActive",
  "message": "El servicio con Id 99 no existe o no está activo"
}
```

---

## 3. Actualizar Cliente con Servicios

### Endpoint
```
PUT /api/customer-update/{customerId}
```

### Headers
```
Authorization: Bearer {token}
Content-Type: application/json
```

### Request Body
```json
{
  "firstName": "Juan",
  "lastName": "Pérez Actualizado",
  "email": "juan.nuevo@email.com",
  "phone1": "1122334455",
  "phone2": null,
  "serviceIds": [2, 4]
}
```

### Campos ServiceIds en Update

| Valor | Comportamiento |
|-------|----------------|
| `null` o no enviado | **No modifica** los servicios existentes |
| `[]` (array vacío) | **Elimina todos** los servicios del cliente |
| `[2, 4]` | **Reemplaza** todos los servicios por los nuevos |

### Response (200 OK)
```json
true
```

---

## 4. Ejemplo de Implementación UI

### Componente de Selección de Servicios (React/TypeScript)

```typescript
interface Service {
  serviceId: number;
  name: string;
}

// Fetch servicios para el dropdown
const fetchServices = async (): Promise<Service[]> => {
  const response = await fetch('/api/services-list', {
    headers: { 'Authorization': `Bearer ${token}` }
  });
  return response.json();
};

// En el formulario de cliente
const [selectedServiceIds, setSelectedServiceIds] = useState<number[]>([]);
const [services, setServices] = useState<Service[]>([]);

useEffect(() => {
  fetchServices().then(setServices);
}, []);

// Al enviar el formulario
const handleSubmit = async (customerData: CustomerForm) => {
  const payload = {
    ...customerData,
    serviceIds: selectedServiceIds.length > 0 ? selectedServiceIds : null
  };

  await fetch('/api/customer-create', {
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${token}`,
      'Content-Type': 'application/json'
    },
    body: JSON.stringify(payload)
  });
};
```

### Consideraciones UI

1. **Multiselect**: Usar un componente multiselect para permitir seleccionar varios servicios
2. **Carga inicial**: Al editar un cliente, cargar los servicios actualmente asociados (requiere endpoint adicional si no existe)
3. **Validación**: Los IDs duplicados se ignoran automáticamente en el backend
4. **Feedback de error**: Mostrar mensaje claro si un servicio seleccionado ya no está activo

---

## 5. Tipos TypeScript

```typescript
// DTOs
interface ServiceIdNameDto {
  serviceId: number;
  name: string;
}

interface CustomerCreateRequest {
  firstName: string;
  lastName: string;
  email: string;
  documentNumber: string;
  phone1: string;
  phone2?: string | null;
  serviceIds?: number[] | null;
}

interface CustomerUpdateRequest {
  firstName: string;
  lastName: string;
  email: string;
  phone1: string;
  phone2?: string | null;
  serviceIds?: number[] | null;
}
```

---

## 6. Notas Importantes

- Los `serviceIds` duplicados en el array se ignoran (se aplica `Distinct`)
- Solo se pueden asociar servicios con `Status = Active`
- La asociación se guarda en la tabla `ServiceCustomer`
- El endpoint `services-list` requiere rol `Admin`
