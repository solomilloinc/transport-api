using Transport.SharedKernel.Contracts.City;
using Transport.SharedKernel.Contracts.Direction;

namespace Transport.SharedKernel.Contracts.Trip;

public record TripReportResponseDto(
    int TripId,
    string Description,
    int OriginCityId,
    string OriginCityName,
    int DestinationCityId,
    string DestinationCityName,
    string Status,
    List<TripPriceReportDto> Prices,
    List<CityDirectionsDto> RelevantCities,
    // Frontend-ready options
    List<PickupOptionDto> PickupOptions,
    List<DropoffOptionDto> DropoffOptionsIda,
    /// <summary>
    /// Precios IdaVuelta (descuento round-trip). Sólo aplican cuando el tenant tiene
    /// <see cref="Transport.Domain.Tenants.TenantConfig.RoundTripRequiresSameDay"/> en true
    /// Y las dos reservas seleccionadas (outbound y return) son del MISMO día calendario.
    /// Si las fechas difieren, el frontend debe sumar precios de <see cref="DropoffOptionsIda"/>
    /// para cada pierna y mandar precio Ida en el wrapper. Si no, el server validará
    /// y rechazará con PriceNotAvailable.
    /// </summary>
    List<DropoffOptionDto> DropoffOptionsIdaVuelta,
    List<TripPickupStopReportDto>? StopSchedules = null);

public record TripPriceReportDto(
    int TripPriceId,
    int CityId,
    string CityName,
    int? DirectionId,
    string? DirectionName,
    int ReserveTypeId,
    string ReserveTypeName,
    decimal Price,
    int Order,
    string Status,
    string DisplayName,
    bool IsMainDestination);

/// <summary>
/// Pickup option for frontend combo - directions from origin city
/// </summary>
public record PickupOptionDto(
    int DirectionId,
    string DisplayName,
    TimeSpan? PickupTimeOffset = null);

/// <summary>
/// Dropoff option grouped by city - contains price and all directions for that city
/// </summary>
public record DropoffOptionDto(
    int CityId,
    string CityName,
    decimal Price,
    bool IsMainDestination,
    List<DropoffDirectionDto> Directions);

/// <summary>
/// Direction within a dropoff city
/// </summary>
public record DropoffDirectionDto(
    int DirectionId,
    string DisplayName);
