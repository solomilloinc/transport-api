using Transport.Domain.Reserves;
using Transport.SharedKernel;

namespace Transport.Domain.Customers;

public class CustomerBookingHistory : Entity
{
    public int CustomerBookingHistoryId { get; set; }
    public int CustomerId { get; set; }
    public int ReserveId { get; set; }
    public BookingRoleEnum Role { get; set; } // Booker, Passenger, Both
    public DateTime BookingDate { get; set; }

    public Customer Customer { get; set; }
    public Reserve Reserve { get; set; }
}

public enum BookingRoleEnum
{
    Booker = 1,     // Solo hizo la reserva
    Passenger = 2,  // Solo viajó
    Both = 3        // Reservó y viajó
}