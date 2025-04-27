using Transport.SharedKernel;

namespace Transport.Domain.Cities;

public static class CityError
{
    public static readonly Error CityNotFound = Error.Validation(
          "City.CityId",
          "La ciudad no existe");

    public static readonly Error CityAlreadyExist = Error.Validation(
        "City.Code",
        "Hay una Ciudad que ya éxiste con esta información");
}
