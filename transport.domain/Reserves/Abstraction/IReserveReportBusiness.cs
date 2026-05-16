using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Passenger;
using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.Domain.Reserves.Abstraction;

public interface IReserveReportBusiness
{
    Task<Result<PagedReportResponseDto<ReserveReportResponseDto>>> GetReserveReport(
        DateTime reserveDate,
        PagedReportRequestDto<ReserveReportFilterRequestDto> requestDto);

    Task<Result<ReserveGroupedPagedReportResponseDto>> GetReserveReport(
        PagedReportRequestDto<ReserveReportFilterRequestDto> requestDto);

    Task<Result<PagedReportResponseDto<PassengerReserveReportResponseDto>>> GetReservePassengerReport(
        int reserveId,
        PagedReportRequestDto<PassengerReserveReportFilterRequestDto> requestDto);

    Task<Result<PagedReportResponseDto<ReservePaymentSummaryResponseDto>>> GetReservePaymentSummary(
        int reserveId,
        PagedReportRequestDto<ReservePaymentSummaryFilterRequestDto> requestDto);
}
