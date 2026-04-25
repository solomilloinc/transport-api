using Transport.Domain.Reserves;
using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.Business.ReserveBusiness.Internal;

/// <summary>
/// Centraliza las dos estrategias heredadas para elegir la "reserva principal"
/// que recibe el pago padre. Admin y externo usan criterios distintos: dejar
/// las dos visibles facilita una eventual unificación futura.
/// </summary>
internal static class MainReserveSelector
{
    public static int ByMinReserveId(IEnumerable<PassengerReserveCreateRequestDto> items)
        => items.Min(i => i.ReserveId);

    public static int ByMinReserveId(IEnumerable<PassengerReserveExternalCreateRequestDto> items)
        => items.Min(i => i.ReserveId);

    public static Reserve ByEarliestDateThenId(IEnumerable<Reserve> reserves)
        => reserves.OrderBy(r => r.ReserveDate).ThenBy(r => r.ReserveId).First();
}
