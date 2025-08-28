using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Transport.Domain.Customers;
using Transport.Domain.Directions;
using Transport.Domain.Reserves;
using Transport.SharedKernel;

namespace Transport.Domain.Passengers;

public class Passenger : Entity, IAuditable
{
    public int PassengerId { get; set; }
    public int ReserveId { get; set; }

    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string DocumentNumber { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }

    // Información del viaje
    public int? PickupLocationId { get; set; }
    public int? DropoffLocationId { get; set; }
    public string? PickupAddress { get; set; }
    public string? DropoffAddress { get; set; }
    public bool HasTraveled { get; set; }
    public decimal Price { get; set; }
    public PassengerStatusEnum Status { get; set; }

    // Relación opcional con Customer (si el pasajero es cliente)
    public int? CustomerId { get; set; }

    // Navegación
    public Reserve Reserve { get; set; }
    public Customer? Customer { get; set; }
    public Direction? PickupLocation { get; set; }
    public Direction? DropoffLocation { get; set; }

    // Auditoría
    public string CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedDate { get; set; }
}
