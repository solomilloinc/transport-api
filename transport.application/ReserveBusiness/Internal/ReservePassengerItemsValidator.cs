using Transport.Domain.Reserves;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.Business.ReserveBusiness.Internal;

/// <summary>
/// Reglas de combinación de items para el flujo externo (usuario final).
/// Con dos reservas (ida/vuelta): por defecto Ida + IdaVuelta; si las fechas de reserva
/// son días distintos y el tenant usa combo solo mismo día, también Ida + Ida (alineado
/// con <see cref="ReservePricingResolver.ResolveAppliedReserveTypeAsync"/>).
/// </summary>
internal static class ReservePassengerItemsValidator
{
    public static Result ValidateUserReserveCombination(
        List<PassengerReserveExternalCreateRequestDto> items,
        IReadOnlyDictionary<int, DateTime>? reserveDatesById = null,
        bool roundTripSameDayOnly = true)
    {
        if (items == null || items.Count == 0)
            return Result.Failure(ReserveError.InvalidReserveCombination("No hay ítems para validar."));

        // 1) Agrupar por reserva y obtener los tipos distintos por cada reserva
        var byReserve = items
            .GroupBy(i => i.ReserveId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(i => (ReserveTypeIdEnum)i.ReserveTypeId).Distinct().ToList()
            );

        // 2) Dentro de una misma reserva no puede haber más de un tipo
        var mixedTypeReserveIds = byReserve
            .Where(kv => kv.Value.Count > 1)
            .Select(kv => kv.Key)
            .ToList();

        if (mixedTypeReserveIds.Any())
            return Result.Failure(ReserveError.InvalidReserveCombination(
                $"La(s) reserva(s) {string.Join(", ", mixedTypeReserveIds)} tiene(n) más de un tipo asignado."));

        // 3) Máximo 2 reservas
        var distinctReserveIds = byReserve.Keys.ToList();
        if (distinctReserveIds.Count > 2)
            return Result.Failure(ReserveError.InvalidReserveCombination(
                "Solo se permite reservar hasta 2 reservas: ida y vuelta."));

        // 4) Tomar el tipo (único) de cada reserva
        var typesPerReserve = byReserve.ToDictionary(kv => kv.Key, kv => kv.Value.Single());

        if (distinctReserveIds.Count == 1)
        {
            var singleType = typesPerReserve.Values.Single();
            if (singleType == ReserveTypeIdEnum.IdaVuelta)
                return Result.Failure(ReserveError.InvalidReserveCombination(
                    "No se puede reservar únicamente la vuelta sin haber reservado ida."));
            return Result.Success();
        }

        // 5) Dos reservas: Ida + IdaVuelta (combo / contrato clásico), o Ida + Ida si vuelta es otro día y el tenant fuerza combo solo mismo día
        var typeSet = new HashSet<ReserveTypeIdEnum>(typesPerReserve.Values);
        if (typeSet.SetEquals(new[] { ReserveTypeIdEnum.Ida, ReserveTypeIdEnum.IdaVuelta }))
            return Result.Success();

        if (AllowsTwoLegDifferentDayAsTwoIda(typesPerReserve, distinctReserveIds, reserveDatesById, roundTripSameDayOnly))
            return Result.Success();

        return Result.Failure(ReserveError.InvalidReserveCombination(
            "La combinación válida es Ida + IdaVuelta (mismo día o distinto), o Ida + Ida cuando la vuelta es otro día calendario."));
    }

    private static bool AllowsTwoLegDifferentDayAsTwoIda(
        IReadOnlyDictionary<int, ReserveTypeIdEnum> typesPerReserve,
        IReadOnlyList<int> distinctReserveIds,
        IReadOnlyDictionary<int, DateTime>? reserveDatesById,
        bool roundTripSameDayOnly)
    {
        if (distinctReserveIds.Count != 2)
            return false;

        if (!roundTripSameDayOnly)
            return false;

        if (!typesPerReserve.Values.All(t => t == ReserveTypeIdEnum.Ida))
            return false;

        if (reserveDatesById is null)
            return false;

        var id0 = distinctReserveIds[0];
        var id1 = distinctReserveIds[1];
        if (!reserveDatesById.TryGetValue(id0, out var d0) || !reserveDatesById.TryGetValue(id1, out var d1))
            return false;

        return d0.Date != d1.Date;
    }
}
