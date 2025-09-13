namespace Transport.Domain.Reserves;

public enum ReserveSlotLockStatus
{
    Active = 1,     // Bloqueo activo
    Expired = 2,    // Expirado
    Used = 3,       // Convertido a reserva real
    Cancelled = 4   // Cancelado por el usuario
}