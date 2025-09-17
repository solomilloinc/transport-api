using Transport.Domain.Customers;
using Transport.SharedKernel;

namespace Transport.Domain.Reserves;

public class ReserveSlotLock : Entity, IAuditable
{
    public int ReserveSlotLockId { get; set; }
    public string LockToken { get; set; } = null!; // GUID único para el frontend

    // Referencias a reservas (ida y vuelta si aplica)
    public int OutboundReserveId { get; set; }
    public int? ReturnReserveId { get; set; }

    public int SlotsLocked { get; set; } // Cantidad de cupos bloqueados
    public DateTime ExpiresAt { get; set; }
    public ReserveSlotLockStatus Status { get; set; }

    // Optimistic Concurrency Control
    public byte[] RowVersion { get; set; } = null!;

    // Información del usuario que hace la reserva
    public string? UserEmail { get; set; }
    public string? UserDocumentNumber { get; set; }
    public int? CustomerId { get; set; }

    // Auditoría
    public string CreatedBy { get; set; } = null!;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedDate { get; set; }

    // Navegación
    public Reserve OutboundReserve { get; set; } = null!;
    public Reserve? ReturnReserve { get; set; }
    public Customer? Customer { get; set; }
}