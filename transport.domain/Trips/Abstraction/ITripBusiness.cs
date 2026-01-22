using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Trip;

namespace Transport.Domain.Trips.Abstraction;

public interface ITripBusiness
{
    Task<Result<int>> CreateTrip(TripCreateDto dto);
    Task<Result<bool>> UpdateTrip(int tripId, TripCreateDto dto);
    Task<Result<bool>> DeleteTrip(int tripId);
    Task<Result<bool>> UpdateTripStatus(int tripId, EntityStatusEnum status);
    Task<Result<PagedReportResponseDto<TripReportResponseDto>>> GetTripReport(PagedReportRequestDto<TripReportFilterDto> request);
    Task<Result<TripReportResponseDto>> GetTripById(int tripId);

    // Price management
    Task<Result<int>> AddPrice(TripPriceCreateDto dto);
    Task<Result<bool>> UpdatePrice(int tripPriceId, TripPriceUpdateDto dto);
    Task<Result<bool>> DeletePrice(int tripPriceId);
    Task<Result<bool>> UpdatePricesByPercentage(PriceMassiveUpdateDto dto);

    // Price lookup for reservations
    Task<Result<decimal>> GetPriceForReservation(int tripId, int? dropoffCityId, int? dropoffDirectionId, int reserveTypeId);
}
