using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Customer;
using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.Domain.Reserves.Abstraction;

public interface IReserveBusiness
{
    Task<Result<bool>> CreatePassengerReserves(CustomerReserveCreateRequestWrapperDto dto);

    Task<Result<CreateReserveExternalResult>> CreatePassengerReservesExternal(CustomerReserveCreateRequestWrapperExternalDto dto);

    Task<Result<PagedReportResponseDto<ReservePriceReportResponseDto>>>
     GetReservePriceReport(PagedReportRequestDto<ReservePriceReportFilterRequestDto> requestDto);

    Task<Result<PagedReportResponseDto<ReserveReportResponseDto>>>
    GetReserveReport(DateTime reserveDate, PagedReportRequestDto<ReserveReportFilterRequestDto> requestDto);

    Task<Result<PagedReportResponseDto<CustomerReserveReportResponseDto>>> GetReserveCustomerReport(int reserveId, PagedReportRequestDto<CustomerReserveReportFilterRequestDto> requestDto);

    Task<Result<bool>> UpdateReserveAsync(int reserveId, ReserveUpdateRequestDto request);
    Task<Result<bool>> CreatePaymentsAsync(
    int reserveId,
    int customerId,
    List<CreatePaymentRequestDto> payments);

    Task<Result<bool>> UpdateCustomerReserveAsync(int customerReserveId, CustomerReserveUpdateRequestDto request);
    Task<Result<bool>> UpdateReservePaymentsByExternalId(string externalId);
    Task<Result<ReserveGroupedPagedReportResponseDto>> GetReserveReport(PagedReportRequestDto<ReserveReportFilterRequestDto> requestDto);
}
