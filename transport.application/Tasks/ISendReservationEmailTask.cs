using Transport.Domain.Reserves;

namespace Transport.Business.Tasks;

public interface ISendReservationEmailTask
{
    Task ExecuteAsync(CustomerReserveCreatedEvent @event);
}
