using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.Business.ReserveBusiness.Internal;

/// <summary>
/// Suma el precio total de un conjunto de items enriquecidos aplicando dedup
/// de combo IdaVuelta: cuando el combo aplicó (mismo día), el precio aparece
/// replicado en ambas patas y solo se cuenta la principal.
/// </summary>
internal sealed class ReserveTotalCalculator
{
    public decimal Compute(IReadOnlyList<EnrichedPassengerItem> enriched)
    {
        decimal total = 0m;
        foreach (var item in enriched)
        {
            if (!item.IsComboReturnLeg)
                total += item.ResolvedPrice;
        }
        return total;
    }

    /// <summary>
    /// Items que se mandan a MercadoPago: MP suma el UnitPrice de cada item para
    /// calcular el cobro al usuario, así que las patas combo duplicadas se excluyen
    /// y el precio se reescribe con el server-validated (no el que vino del front).
    /// </summary>
    public List<PassengerReserveExternalCreateRequestDto> BuildMpItems(IReadOnlyList<EnrichedPassengerItem> enriched)
    {
        var result = new List<PassengerReserveExternalCreateRequestDto>(enriched.Count);
        foreach (var item in enriched)
        {
            if (item.IsComboReturnLeg) continue;
            if (item.ExternalDto is null) continue;
            result.Add(item.ExternalDto with { Price = item.ResolvedPrice });
        }
        return result;
    }
}
