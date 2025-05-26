using Transport.SharedKernel;

namespace Transport.Domain.Directions;

public static class DirectionError
{
    public static readonly Error DirectionNotFound = Error.Validation(
       "Direction.DirectionId",
       "La Dirección no existe");
}
