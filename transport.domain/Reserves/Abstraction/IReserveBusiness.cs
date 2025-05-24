using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Customer;
using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.Domain.Reserves.Abstraction;

public interface IReserveBusiness
{
    Task<Result<bool>> CreatePassengerReserves(CustomerReserveCreateRequestWrapperDto dto);

    Task<Result<PagedReportResponseDto<ReservePriceReportResponseDto>>>
     GetReservePriceReport(PagedReportRequestDto<ReservePriceReportFilterRequestDto> requestDto);

    Task<Result<PagedReportResponseDto<ReserveReportResponseDto>>>
    GetReserveReport(DateTime reserveDate, PagedReportRequestDto<ReserveReportFilterRequestDto> requestDto);

    Task<Result<PagedReportResponseDto<CustomerReserveReportResponseDto>>> GetReserveCustomerReport(int reserveId, PagedReportRequestDto<CustomerReserveReportFilterRequestDto> requestDto);
}
