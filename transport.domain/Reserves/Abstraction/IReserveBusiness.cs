using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Passenger;
using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.Domain.Reserves.Abstraction;

public interface IReserveBusiness
{
    Task<Result<bool>> CreatePassengerReserves(PassengerReserveCreateRequestWrapperDto dto);

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

    // Resumen de pagos por reserva
    Task<Result<PagedReportResponseDto<ReservePaymentSummaryResponseDto>>> GetReservePaymentSummary(
        int reserveId,
        PagedReportRequestDto<ReservePaymentSummaryFilterRequestDto> requestDto);

    // Métodos para bloqueo de cupos
    Task<Result<LockReserveSlotsResponseDto>> LockReserveSlots(LockReserveSlotsRequestDto request);
    Task<Result<CreateReserveExternalResult>> CreatePassengerReservesWithLock(CreateReserveWithLockRequestDto request);
    Task<Result<bool>> CancelReserveSlotLock(string lockToken);
    Task<Result<bool>> CleanupExpiredReserveSlotLocks();
}
