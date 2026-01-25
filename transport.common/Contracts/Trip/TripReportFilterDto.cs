using Transport.SharedKernel;

namespace Transport.SharedKernel.Contracts.Trip;

public record TripReportFilterDto(
    int? OriginCityId,
    int? DestinationCityId,
    EntityStatusEnum? Status);
