using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.Domain.Reserves.Abstraction;

public interface IReserveBusiness
{
    Task<Result<PagedReportResponseDto<ReserveReportResponseDto>>>
     GetReserveReport(PagedReportRequestDto<ReserveReportFilterRequestDto> requestDto);
}
