using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Passenger;
using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.Domain.Reserves.Abstraction;

public interface IReserveBusiness
{
    Task<Result<bool>> CreatePassengerReserves(PassengerReserveCreateRequestWrapperDto dto);

    Task<Result<CreateReserveExternalResult>> CreatePassengerReservesExternal(PassengerReserveCreateRequestWrapperExternalDto dto);

    Task<Result<PagedReportResponseDto<ReservePriceReportResponseDto>>>
     GetReservePriceReport(PagedReportRequestDto<ReservePriceReportFilterRequestDto> requestDto);

    Task<Result<PagedReportResponseDto<ReserveReportResponseDto>>>
    GetReserveReport(DateTime reserveDate, PagedReportRequestDto<ReserveReportFilterRequestDto> requestDto);

    Task<Result<PagedReportResponseDto<PassengerReserveReportResponseDto>>> GetReservePassengerReport(int reserveId, PagedReportRequestDto<PassengerReserveReportFilterRequestDto> requestDto);

    Task<Result<bool>> UpdateReserveAsync(int reserveId, ReserveUpdateRequestDto request);
    Task<Result<bool>> CreatePaymentsAsync(
    int customerId,
    int reserveId,
    List<CreatePaymentRequestDto> payments);

    Task<Result<bool>> UpdatePassengerReserveAsync(int customerReserveId, PassengerReserveUpdateRequestDto request);
    Task<Result<bool>> UpdateReservePaymentsByExternalId(string externalId);
    Task<Result<ReserveGroupedPagedReportResponseDto>> GetReserveReport(PagedReportRequestDto<ReserveReportFilterRequestDto> requestDto);
}
