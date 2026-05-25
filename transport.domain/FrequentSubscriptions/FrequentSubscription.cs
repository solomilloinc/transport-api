using Transport.Domain.Customers;
using Transport.Domain.Directions;
using Transport.Domain.Reserves;
using Transport.Domain.Services;
using Transport.SharedKernel;

namespace Transport.Domain.FrequentSubscriptions;

public class FrequentSubscription : Entity, IAuditable, ITenantScoped
{
    public int FrequentSubscriptionId { get; set; }
    public int TenantId { get; set; }

    public int CustomerId { get; set; }
    public ReserveTypeIdEnum ReserveTypeId { get; set; }

    public int OutboundServiceId { get; set; }
    public int? InboundServiceId { get; set; }

    public int OutboundPickupLocationId { get; set; }
    public int OutboundDropoffLocationId { get; set; }
    public int? InboundPickupLocationId { get; set; }
    public int? InboundDropoffLocationId { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public EntityStatusEnum Status { get; set; } = EntityStatusEnum.Active;

    public string CreatedBy { get; set; } = null!;
    public string? UpdatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }

    public Customer Customer { get; set; } = null!;
    public Service OutboundService { get; set; } = null!;
    public Service? InboundService { get; set; }
    public Direction OutboundPickupLocation { get; set; } = null!;
    public Direction OutboundDropoffLocation { get; set; } = null!;
    public Direction? InboundPickupLocation { get; set; }
    public Direction? InboundDropoffLocation { get; set; }
}
