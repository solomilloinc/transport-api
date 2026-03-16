using Transport.Domain.Reserves;
using Transport.Domain.Users;
using Transport.SharedKernel;

namespace Transport.Domain.CashBoxes;

public class CashBox : Entity, IAuditable, ITenantScoped
{
    public int CashBoxId { get; set; }
    public int TenantId { get; set; }
    public string? Description { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public CashBoxStatusEnum Status { get; set; }

    public int OpenedByUserId { get; set; }
    public int? ClosedByUserId { get; set; }
    public int? ReserveId { get; set; }

    // Navegacion
    public User OpenedByUser { get; set; } = null!;
    public User? ClosedByUser { get; set; }
    public Reserve? Reserve { get; set; }
    public ICollection<ReservePayment> Payments { get; set; } = new List<ReservePayment>();

    // Auditoria
    public string CreatedBy { get; set; } = null!;
    public string? UpdatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }
}
