using Transport.Domain.Customers;
using Transport.Domain.Passengers;

namespace Transport.Business.ReserveBusiness.Internal;

/// <summary>
/// Construye Passenger a partir de un EnrichedPassengerItem ya resuelto.
/// Hay dos variantes (admin y externo) porque el origen de los datos del
/// pasajero difiere: en admin todos los pasajeros son el payer; en externo
/// cada item trae sus propios datos personales.
/// </summary>
internal sealed class ReservePassengerFactory
{
    public Passenger BuildAdmin(EnrichedPassengerItem item, Customer payer)
    {
        var dto = item.AdminDto!;
        return new Passenger
        {
            ReserveId = item.ReserveId,
            ReserveRelatedId = item.InferredReserveRelatedId,
            PickupLocationId = dto.PickupLocationId,
            DropoffLocationId = dto.DropoffLocationId,
            PickupAddress = item.PickupDirection?.Name,
            DropoffAddress = item.DropoffDirection?.Name,
            HasTraveled = dto.HasTraveled,
            Price = item.ResolvedPrice,
            Status = PassengerStatusEnum.PendingPayment,
            CustomerId = payer.CustomerId,
            DocumentNumber = payer.DocumentNumber,
            FirstName = payer.FirstName,
            LastName = payer.LastName,
            Phone = $"{payer.Phone1} / {payer.Phone2}",
            Email = payer.Email,
        };
    }

    public Passenger BuildExternal(EnrichedPassengerItem item, bool hasExternalPayment)
    {
        var dto = item.ExternalDto!;
        return new Passenger
        {
            ReserveId = item.ReserveId,
            ReserveRelatedId = item.InferredReserveRelatedId,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            DocumentNumber = dto.DocumentNumber,
            Email = dto.Email,
            Phone = dto.Phone1,
            PickupLocationId = dto.PickupLocationId,
            DropoffLocationId = dto.DropoffLocationId,
            PickupAddress = item.PickupDirection?.Name,
            DropoffAddress = item.DropoffDirection?.Name,
            HasTraveled = false,
            Price = item.ResolvedPrice,
            Status = hasExternalPayment ? PassengerStatusEnum.Confirmed : PassengerStatusEnum.PendingPayment,
            CustomerId = item.ExistingCustomer?.CustomerId,
        };
    }
}
