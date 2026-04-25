using Transport.Domain.Customers;
using Transport.Domain.Directions;
using Transport.Domain.Reserves;
using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.Business.ReserveBusiness.Internal;

/// <summary>
/// DTO interno con todos los datos resueltos para construir un Passenger.
/// El Enricher lo arma una sola vez antes del loop, evitando que el orquestador
/// haga queries y cálculos por iteración.
/// </summary>
internal sealed class EnrichedPassengerItem
{
    public PassengerReserveCreateRequestDto? AdminDto { get; init; }
    public PassengerReserveExternalCreateRequestDto? ExternalDto { get; init; }

    public required Reserve Reserve { get; init; }
    public required decimal ResolvedPrice { get; init; }
    public required ReserveTypeIdEnum AppliedReserveType { get; init; }
    public required bool IsComboReturnLeg { get; init; }
    public Direction? PickupDirection { get; init; }
    public Direction? DropoffDirection { get; init; }
    public int? InferredReserveRelatedId { get; init; }

    /// <summary>Existing customer matched by document number (external flow only). Null if none.</summary>
    public Customer? ExistingCustomer { get; init; }

    public int ReserveId => Reserve.ReserveId;
    public int RequestedReserveTypeId => AdminDto?.ReserveTypeId ?? ExternalDto!.ReserveTypeId;
}
